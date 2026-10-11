using System.Data;
using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class CitaRepository : ICitaRepository
{
    private readonly DapperContext _context;

    public CitaRepository(DapperContext context) => _context = context;

    // Tres JOIN: la cita necesita datos de la remision, del residente
    // y del especialista para que la pantalla no tenga que pedirlos aparte.
    private const string SqlBase = @"
        SELECT  c.Id,
                c.RemisionId,
                rem.ResidenteId,
                r.Nombres + ' ' + r.Apellidos  AS ResidenteNombre,
                rem.EspecialidadId,
                esp.Nombre                     AS EspecialidadSolicitada,
                rem.Sintomatologia,
                c.EspecialistaId,
                u.NombreCompleto               AS EspecialistaNombre,
                espU.Nombre                    AS EspecialistaEspecialidad,
                c.BloqueHorario,
                c.Diagnostico,
                c.CostoConsulta,
                CASE c.Estado
                     WHEN 0 THEN 'Programada'
                     WHEN 1 THEN 'En evaluacion'
                     WHEN 2 THEN 'Esperando anexos'
                     WHEN 3 THEN 'Bitacora completada'
                     ELSE 'Costos calculados' END AS Estado,
                c.Liquidada,
                c.RequiereLaboratorio,
                c.RequiereFarmacia
        FROM CitasFundacion c
        INNER JOIN Remisiones     rem  ON rem.Id  = c.RemisionId
        INNER JOIN Residentes     r    ON r.Id    = rem.ResidenteId
        INNER JOIN Usuarios       u    ON u.Id    = c.EspecialistaId
        INNER JOIN Especialidades esp  ON esp.Id  = rem.EspecialidadId
        LEFT  JOIN Especialidades espU ON espU.Id = u.EspecialidadId";

    public async Task<IEnumerable<CitaDto>> ObtenerTodasAsync(EstadoCita? estado, int? especialistaId)
    {
        var condiciones = new List<string>();
        if (estado.HasValue)         condiciones.Add("c.Estado = @Estado");
        if (especialistaId.HasValue) condiciones.Add("c.EspecialistaId = @EspecialistaId");

        string where = condiciones.Count > 0 ? " WHERE " + string.Join(" AND ", condiciones) : "";
        string sql = SqlBase + where + " ORDER BY c.BloqueHorario;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<CitaDto>(sql, new
        {
            Estado = estado.HasValue ? (byte)estado.Value : (byte)0,
            EspecialistaId = especialistaId ?? 0
        });
    }

    public async Task<CitaDto?> ObtenerPorIdAsync(int id)
    {
        string sql = SqlBase + " WHERE c.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<CitaDto>(sql, new { Id = id });
    }

    /// <summary>
    /// Crea la cita y marca la remision como Programada. Las dos operaciones
    /// van en una transaccion: una cita sin su remision actualizada dejaria
    /// la solicitud apareciendo como pendiente para la fundacion.
    /// </summary>
    public async Task<int> AsignarAsync(CitaFundacion cita)
    {
        using var conexion = _context.CreateConnection();
        conexion.Open();                               // obligatorio antes de BeginTransaction
        using var transaccion = conexion.BeginTransaction();

        try
        {
            const string sqlCita = @"
                INSERT INTO CitasFundacion (RemisionId, EspecialistaId, BloqueHorario,
                                            CostoConsulta, Estado, Liquidada)
                VALUES (@RemisionId, @EspecialistaId, @BloqueHorario,
                        @CostoConsulta, 0, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int idCita = await conexion.QuerySingleAsync<int>(sqlCita, cita, transaccion);

            // Estado 1 = Programada
            const string sqlRemision = "UPDATE Remisiones SET Estado = 1 WHERE Id = @RemisionId;";
            await conexion.ExecuteAsync(sqlRemision, new { cita.RemisionId }, transaccion);

            transaccion.Commit();
            return idCita;
        }
        catch
        {
            transaccion.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Asienta el diagnostico y deja escrito que indico el especialista.
    /// Guardar las dos banderas es lo que permite despues saber cuando
    /// la bitacora quedo completa: antes el dato se usaba para elegir el
    /// estado y se descartaba, y la cita se quedaba esperando algo que
    /// nadie sabia que era.
    /// </summary>
    public async Task<bool> RegistrarDiagnosticoAsync(
        int id, string diagnostico, bool requiereLaboratorio, bool requiereFarmacia)
    {
        // 2 = EsperandoAnexos, 3 = BitacoraCompletada.
        // Sin anexos solicitados la consulta cierra en el acto.
        byte nuevoEstado = (requiereLaboratorio || requiereFarmacia) ? (byte)2 : (byte)3;

        const string sql = @"
            UPDATE CitasFundacion
            SET Diagnostico         = @Diagnostico,
                Estado              = @Estado,
                RequiereLaboratorio = @RequiereLaboratorio,
                RequiereFarmacia    = @RequiereFarmacia
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new
        {
            Id = id,
            Diagnostico = diagnostico,
            Estado = nuevoEstado,
            RequiereLaboratorio = requiereLaboratorio,
            RequiereFarmacia = requiereFarmacia
        }) > 0;
    }

    public async Task<IEnumerable<EspecialistaDto>> ObtenerEspecialistasAsync()
    {
        const string sql = @"
            SELECT  u.Id,
                    u.NombreCompleto,
                    u.EspecialidadId,
                    e.Nombre AS Especialidad
            FROM Usuarios u
            INNER JOIN Especialidades e ON e.Id = u.EspecialidadId
            WHERE u.Rol = 'Especialista' AND u.Activo = 1
            ORDER BY e.Nombre, u.NombreCompleto;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<EspecialistaDto>(sql);
    }

    /// <summary>
    /// Un especialista no puede tener dos citas en el mismo bloque.
    /// Comparo hasta el minuto, ignorando segundos.
    /// </summary>
    public async Task<bool> HorarioOcupadoAsync(int especialistaId, DateTime bloque, int? excluirCitaId = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM CitasFundacion
            WHERE EspecialistaId = @EspecialistaId
              AND DATEDIFF(MINUTE, BloqueHorario, @Bloque) = 0
              AND (@ExcluirId IS NULL OR Id <> @ExcluirId);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new
        {
            EspecialistaId = especialistaId,
            Bloque = bloque,
            ExcluirId = excluirCitaId
        }) > 0;
    }

    public async Task<bool> RemisionYaTieneCitaAsync(int remisionId)
    {
        const string sql = "SELECT COUNT(1) FROM CitasFundacion WHERE RemisionId = @RemisionId;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { RemisionId = remisionId }) > 0;
    }
}
