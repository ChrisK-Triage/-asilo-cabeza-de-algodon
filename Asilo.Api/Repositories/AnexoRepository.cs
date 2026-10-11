using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class AnexoRepository : IAnexoRepository
{
    private readonly DapperContext _context;

    public AnexoRepository(DapperContext context) => _context = context;

    // ==================== LABORATORIO ====================

    private const string SqlOrdenes = @"
        SELECT  o.Id,
                o.CitaId,
                rem.ResidenteId,
                r.Nombres + ' ' + r.Apellidos AS ResidenteNombre,
                u.NombreCompleto              AS EspecialistaNombre,
                o.TipoExamen,
                o.Resultado,
                o.CostoBruto,
                o.FechaSolicitud,
                o.FechaResultado,
                CASE o.Estado WHEN 0 THEN 'Pendiente' ELSE 'Completada' END AS Estado,
                DATEDIFF(DAY, o.FechaSolicitud, GETDATE()) AS DiasPendiente
        FROM OrdenesLaboratorio o
        INNER JOIN CitasFundacion c ON c.Id   = o.CitaId
        INNER JOIN Remisiones   rem ON rem.Id = c.RemisionId
        INNER JOIN Residentes     r ON r.Id   = rem.ResidenteId
        INNER JOIN Usuarios       u ON u.Id   = c.EspecialistaId";

    public async Task<IEnumerable<OrdenDto>> ObtenerOrdenesAsync(EstadoOrden? estado, int? citaId)
    {
        var condiciones = new List<string>();
        if (estado.HasValue) condiciones.Add("o.Estado = @Estado");
        if (citaId.HasValue) condiciones.Add("o.CitaId = @CitaId");

        string where = condiciones.Count > 0 ? " WHERE " + string.Join(" AND ", condiciones) : "";
        string sql = SqlOrdenes + where + " ORDER BY o.FechaSolicitud;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<OrdenDto>(sql, new
        {
            Estado = estado.HasValue ? (byte)estado.Value : (byte)0,
            CitaId = citaId ?? 0
        });
    }

    public async Task<OrdenDto?> ObtenerOrdenPorIdAsync(int id)
    {
        string sql = SqlOrdenes + " WHERE o.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<OrdenDto>(sql, new { Id = id });
    }

    public async Task<int> CrearOrdenAsync(OrdenLaboratorio orden)
    {
        // El especialista solo solicita el examen. El costo entra despues,
        // cuando el tecnico carga el resultado.
        const string sql = @"
            INSERT INTO OrdenesLaboratorio (CitaId, TipoExamen, CostoBruto, Estado)
            VALUES (@CitaId, @TipoExamen, 0, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, orden);
    }

    public async Task<bool> CargarResultadoAsync(int id, string resultado, decimal costo)
    {
        const string sql = @"
            UPDATE OrdenesLaboratorio
            SET Resultado      = @Resultado,
                CostoBruto     = @Costo,
                FechaResultado = GETDATE(),
                Estado         = 1
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id, Resultado = resultado, Costo = costo }) > 0;
    }

    // ==================== FARMACIA ====================

    private const string SqlDespachos = @"
        SELECT  d.Id,
                d.CitaId,
                rem.ResidenteId,
                r.Nombres + ' ' + r.Apellidos AS ResidenteNombre,
                d.Medicamento,
                d.Cantidad,
                d.TiempoAplicacion,
                d.CostoUnitario,
                d.CostoTotal,
                d.FechaDespacho
        FROM DespachosFarmacia d
        INNER JOIN CitasFundacion c ON c.Id   = d.CitaId
        INNER JOIN Remisiones   rem ON rem.Id = c.RemisionId
        INNER JOIN Residentes     r ON r.Id   = rem.ResidenteId";

    public async Task<IEnumerable<DespachoDto>> ObtenerDespachosAsync(int? citaId)
    {
        string where = citaId.HasValue ? " WHERE d.CitaId = @CitaId" : "";
        string sql = SqlDespachos + where + " ORDER BY d.FechaDespacho DESC;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<DespachoDto>(sql, new { CitaId = citaId ?? 0 });
    }

    public async Task<DespachoDto?> ObtenerDespachoPorIdAsync(int id)
    {
        string sql = SqlDespachos + " WHERE d.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<DespachoDto>(sql, new { Id = id });
    }

    public async Task<int> CrearDespachoAsync(DespachoFarmacia despacho)
    {
        // No inserto CostoTotal: es columna calculada y SQL Server
        // la rechaza si intento escribirla.
        const string sql = @"
            INSERT INTO DespachosFarmacia (CitaId, Medicamento, Cantidad, TiempoAplicacion, CostoUnitario)
            VALUES (@CitaId, @Medicamento, @Cantidad, @TiempoAplicacion, @CostoUnitario);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, despacho);
    }

    // ==================== COMUN ====================

    /// <summary>
    /// Suma lo acumulado en una cita. Este resumen es la base de la
    /// calculadora financiera: consulta + laboratorio + farmacia.
    /// </summary>
    public async Task<ResumenAnexosDto?> ObtenerResumenAsync(int citaId)
    {
        const string sql = @"
            SELECT  c.Id                                      AS CitaId,
                    (SELECT COUNT(*) FROM OrdenesLaboratorio o
                      WHERE o.CitaId = c.Id)                  AS TotalOrdenes,
                    (SELECT COUNT(*) FROM OrdenesLaboratorio o
                      WHERE o.CitaId = c.Id AND o.Estado = 0)  AS OrdenesPendientes,
                    ISNULL((SELECT SUM(o.CostoBruto) FROM OrdenesLaboratorio o
                      WHERE o.CitaId = c.Id), 0)              AS CostoLaboratorio,
                    (SELECT COUNT(*) FROM DespachosFarmacia d
                      WHERE d.CitaId = c.Id)                  AS TotalDespachos,
                    ISNULL((SELECT SUM(d.CostoTotal) FROM DespachosFarmacia d
                      WHERE d.CitaId = c.Id), 0)              AS CostoFarmacia,
                    c.CostoConsulta,
                    c.CostoConsulta
                      + ISNULL((SELECT SUM(o.CostoBruto) FROM OrdenesLaboratorio o
                                WHERE o.CitaId = c.Id), 0)
                      + ISNULL((SELECT SUM(d.CostoTotal) FROM DespachosFarmacia d
                                WHERE d.CitaId = c.Id), 0)    AS CostoTotalCita
            FROM CitasFundacion c
            WHERE c.Id = @CitaId;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<ResumenAnexosDto>(sql, new { CitaId = citaId });
    }

    public async Task<bool> CitaExisteAsync(int citaId)
    {
        const string sql = "SELECT COUNT(1) FROM CitasFundacion WHERE Id = @CitaId;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { CitaId = citaId }) > 0;
    }

    /// <summary>
    /// Un examen o un medicamento solo tienen sentido despues de que el
    /// especialista valoro al residente. Sin diagnostico registrado no hay
    /// quien los haya indicado, y el costo se le facturaria al familiar.
    /// </summary>
    public async Task<bool> CitaTieneDiagnosticoAsync(int citaId)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM CitasFundacion
            WHERE Id = @CitaId
              AND Diagnostico IS NOT NULL
              AND LTRIM(RTRIM(Diagnostico)) <> '';";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { CitaId = citaId }) > 0;
    }

    public async Task<bool> CitaLiquidadaAsync(int citaId)
    {
        const string sql = "SELECT ISNULL(Liquidada, 0) FROM CitasFundacion WHERE Id = @CitaId;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<bool>(sql, new { CitaId = citaId });
    }
}
