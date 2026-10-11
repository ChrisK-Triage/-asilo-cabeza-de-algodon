using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class RemisionRepository : IRemisionRepository
{
    private readonly DapperContext _context;

    public RemisionRepository(DapperContext context) => _context = context;

    // Consulta base. El JOIN con Residentes y Usuarios resuelve los nombres
    // en el mismo viaje a la base, en lugar de N consultas extra.
    private const string SqlBase = @"
        SELECT  rem.Id,
                rem.ResidenteId,
                r.Nombres + ' ' + r.Apellidos  AS ResidenteNombre,
                rem.MedicoGeneralId,
                u.NombreCompleto               AS MedicoNombre,
                rem.Sintomatologia,
                rem.EspecialidadId,
                esp.Nombre                     AS EspecialidadSolicitada,
                rem.EnfermeroId,
                enf.Nombre                     AS EnfermeroAsignado,
                rem.FechaSolicitud,
                CASE rem.Estado
                     WHEN 0 THEN 'Pendiente'
                     WHEN 1 THEN 'Programada'
                     ELSE 'Cancelada' END      AS Estado,
                DATEDIFF(DAY, rem.FechaSolicitud, GETDATE()) AS DiasEspera
        FROM Remisiones rem
        INNER JOIN Residentes     r   ON r.Id   = rem.ResidenteId
        INNER JOIN Usuarios       u   ON u.Id   = rem.MedicoGeneralId
        INNER JOIN Especialidades esp ON esp.Id = rem.EspecialidadId
        -- LEFT: el enfermero es opcional, y un INNER haria desaparecer
        -- de la lista las remisiones que no lo tienen.
        LEFT  JOIN Enfermeros     enf ON enf.Id = rem.EnfermeroId";

    public async Task<IEnumerable<RemisionDto>> ObtenerTodasAsync(EstadoRemision? estado, int? residenteId)
    {
        // Armo el WHERE segun los filtros que vengan. Los valores siempre
        // van parametrizados: nunca concateno el dato dentro del SQL.
        var condiciones = new List<string>();
        if (estado.HasValue)      condiciones.Add("rem.Estado = @Estado");
        if (residenteId.HasValue) condiciones.Add("rem.ResidenteId = @ResidenteId");

        string where = condiciones.Count > 0 ? " WHERE " + string.Join(" AND ", condiciones) : "";
        string sql = SqlBase + where + " ORDER BY rem.FechaSolicitud;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<RemisionDto>(sql, new
        {
            Estado = estado.HasValue ? (byte)estado.Value : (byte)0,
            ResidenteId = residenteId ?? 0
        });
    }

    public async Task<RemisionDto?> ObtenerPorIdAsync(int id)
    {
        string sql = SqlBase + " WHERE rem.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<RemisionDto>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(Remision remision)
    {
        const string sql = @"
            INSERT INTO Remisiones (ResidenteId, MedicoGeneralId, Sintomatologia,
                                    EspecialidadId, EnfermeroId, Estado)
            VALUES (@ResidenteId, @MedicoGeneralId, @Sintomatologia,
                    @EspecialidadId, @EnfermeroId, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, remision);
    }

    public async Task<bool> CambiarEstadoAsync(int id, EstadoRemision estado)
    {
        const string sql = "UPDATE Remisiones SET Estado = @Estado WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id, Estado = (byte)estado }) > 0;
    }

    public async Task<bool> ExisteAsync(int id)
    {
        const string sql = "SELECT COUNT(1) FROM Remisiones WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Id = id }) > 0;
    }
}
