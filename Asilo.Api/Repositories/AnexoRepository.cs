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
        string sql = SqlOrdenes + where + " ORDER BY o.FechaSolicitud DESC, o.Id DESC;";

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

    /// <summary>
    /// Carga el resultado y, en la misma transaccion, revisa si con este
    /// examen la cita ya recibio todo lo que se le pidio. Van juntos a
    /// proposito: si el resultado se guardara y el cierre fallara, la
    /// cita quedaria esperando un examen que ya esta hecho, que es
    /// exactamente el defecto que tenia el sistema antes.
    /// </summary>
    public async Task<bool> CargarResultadoAsync(int id, string resultado, decimal costo)
    {
        using var conexion = _context.CreateConnection();
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();

        try
        {
            const string sqlResultado = @"
                UPDATE OrdenesLaboratorio
                SET Resultado      = @Resultado,
                    CostoBruto     = @Costo,
                    FechaResultado = GETDATE(),
                    Estado         = 1
                WHERE Id = @Id;";

            bool cargado = await conexion.ExecuteAsync(
                sqlResultado,
                new { Id = id, Resultado = resultado, Costo = costo },
                transaccion) > 0;

            if (cargado)
            {
                int citaId = await conexion.ExecuteScalarAsync<int>(
                    "SELECT CitaId FROM OrdenesLaboratorio WHERE Id = @Id;",
                    new { Id = id }, transaccion);

                await conexion.ExecuteAsync(SqlCerrarBitacora, new { CitaId = citaId }, transaccion);
            }

            transaccion.Commit();
            return cargado;
        }
        catch
        {
            transaccion.Rollback();
            throw;
        }
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
                CASE d.Estado WHEN 0 THEN 'Recetado' ELSE 'Despachado' END AS Estado,
                d.FechaReceta,
                d.FechaDespacho,
                DATEDIFF(DAY, d.FechaReceta, GETDATE()) AS DiasPendiente
        FROM DespachosFarmacia d
        INNER JOIN CitasFundacion c ON c.Id   = d.CitaId
        INNER JOIN Remisiones   rem ON rem.Id = c.RemisionId
        INNER JOIN Residentes     r ON r.Id   = rem.ResidenteId";

    public async Task<IEnumerable<DespachoDto>> ObtenerDespachosAsync(int? citaId)
    {
        string where = citaId.HasValue ? " WHERE d.CitaId = @CitaId" : "";
        string sql = SqlDespachos + where + " ORDER BY d.FechaReceta DESC, d.Id DESC;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<DespachoDto>(sql, new { CitaId = citaId ?? 0 });
    }

    public async Task<DespachoDto?> ObtenerDespachoPorIdAsync(int id)
    {
        string sql = SqlDespachos + " WHERE d.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<DespachoDto>(sql, new { Id = id });
    }

    /// <summary>
    /// El especialista receta. No cierra bitacora: recetar no es
    /// entregar, y la cita sigue esperando a farmacia.
    /// </summary>
    public async Task<int> CrearRecetaAsync(DespachoFarmacia receta)
    {
        // No inserto CostoTotal: es columna calculada y SQL Server la
        // rechaza si intento escribirla. Tampoco FechaDespacho, que
        // queda nula hasta que farmacia entregue.
        const string sql = @"
            INSERT INTO DespachosFarmacia
                   (CitaId, Medicamento, Cantidad, TiempoAplicacion, CostoUnitario, Estado)
            VALUES (@CitaId, @Medicamento, @Cantidad, @TiempoAplicacion, 0, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, receta);
    }

    /// <summary>
    /// Farmacia entrega: fija el precio y, en la misma transaccion,
    /// evalua si con esto la cita ya recibio todo lo que se le indico.
    ///
    /// Van juntos por el mismo motivo que en laboratorio. Si la entrega
    /// se guardara y el cierre fallara, la cita quedaria esperando un
    /// medicamento ya entregado, que es el defecto de CF-01 otra vez.
    /// </summary>
    public async Task<bool> DespacharAsync(int id, decimal costoUnitario)
    {
        using var conexion = _context.CreateConnection();
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();

        try
        {
            const string sqlEntrega = @"
                UPDATE DespachosFarmacia
                SET CostoUnitario = @Costo,
                    FechaDespacho = GETDATE(),
                    Estado        = 1
                WHERE Id = @Id AND Estado = 0;";

            bool entregado = await conexion.ExecuteAsync(
                sqlEntrega, new { Id = id, Costo = costoUnitario }, transaccion) > 0;

            if (entregado)
            {
                int citaId = await conexion.ExecuteScalarAsync<int>(
                    "SELECT CitaId FROM DespachosFarmacia WHERE Id = @Id;",
                    new { Id = id }, transaccion);

                await conexion.ExecuteAsync(SqlCerrarBitacora, new { CitaId = citaId }, transaccion);
            }

            transaccion.Commit();
            return entregado;
        }
        catch
        {
            transaccion.Rollback();
            throw;
        }
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
                    (SELECT COUNT(*) FROM DespachosFarmacia d
                      WHERE d.CitaId = c.Id AND d.Estado = 0)  AS DespachosPendientes,
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
    /// Todo lo que necesito para decidir si una cita admite un anexo
    /// nuevo, en una sola consulta. Devuelve null si la cita no existe.
    /// </summary>
    public async Task<EstadoAnexosDto?> ObtenerEstadoAnexosAsync(int citaId)
    {
        const string sql = @"
            SELECT  Id AS CitaId,
                    Estado,
                    CASE WHEN Diagnostico IS NOT NULL
                          AND LTRIM(RTRIM(Diagnostico)) <> ''
                         THEN 1 ELSE 0 END AS TieneDiagnostico,
                    RequiereLaboratorio,
                    RequiereFarmacia,
                    Liquidada
            FROM CitasFundacion
            WHERE Id = @CitaId;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<EstadoAnexosDto>(sql, new { CitaId = citaId });
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

    // ==================== CIERRE DE LA BITACORA ====================

    /// <summary>
    /// La transicion EsperandoAnexos -> BitacoraCompletada del diagrama
    /// de estados. Antes no la ejecutaba nadie y la cita se quedaba
    /// atrapada para siempre.
    ///
    /// El criterio: cierra cuando cada destino que el especialista
    /// solicito ya tiene su registro, y ninguna orden de laboratorio
    /// quedo pendiente de resultado. Lo que no se pidio no se espera.
    ///
    /// Va todo en el WHERE y no en codigo C# para que la decision y la
    /// escritura ocurran en la misma sentencia: si entre consultar y
    /// actualizar entrara otra orden, el estado podria quedar mal.
    /// El UPDATE no hace nada si la cita no esta en estado 2, asi que
    /// llamarlo de mas es inofensivo.
    /// </summary>
    private const string SqlCerrarBitacora = @"
        UPDATE CitasFundacion
        SET Estado = 3
        WHERE Id = @CitaId
          AND Estado = 2
          AND (RequiereLaboratorio = 0
               OR (    EXISTS (SELECT 1 FROM OrdenesLaboratorio o
                                WHERE o.CitaId = @CitaId)
                   AND NOT EXISTS (SELECT 1 FROM OrdenesLaboratorio o
                                    WHERE o.CitaId = @CitaId AND o.Estado = 0)))
          AND (RequiereFarmacia = 0
               OR (    EXISTS (SELECT 1 FROM DespachosFarmacia d
                                WHERE d.CitaId = @CitaId)
                   AND NOT EXISTS (SELECT 1 FROM DespachosFarmacia d
                                    WHERE d.CitaId = @CitaId AND d.Estado = 0)));";

    public async Task<bool> CerrarBitacoraSiCompletaAsync(int citaId)
    {
        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(SqlCerrarBitacora, new { CitaId = citaId }) > 0;
    }
}
