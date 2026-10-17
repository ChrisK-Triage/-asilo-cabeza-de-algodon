using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Dapper;

namespace Asilo.Api.Repositories;

public class ReporteRepository : IReporteRepository
{
    private readonly DapperContext _context;

    public ReporteRepository(DapperContext context) => _context = context;

    // ==================== 1. HISTORIAL CLINICO ====================

    /// <summary>
    /// El expediente completo de un residente: cada consulta con su
    /// diagnostico, los examenes que se le hicieron y lo que se le
    /// despacho.
    ///
    /// Los examenes y los medicamentos se concatenan con STRING_AGG en
    /// lugar de traerlos como filas aparte. Es un reporte para leer, no
    /// para procesar: una linea por consulta se lee de corrido, y tres
    /// consultas separadas que despues hay que cruzar en C# producen lo
    /// mismo con mas trabajo.
    /// </summary>
    public async Task<ReporteHistorialDto?> HistorialClinicoAsync(
        int residenteId, DateTime? desde, DateTime? hasta)
    {
        const string sqlResidente = @"
            SELECT  Nombres + ' ' + Apellidos AS Residente,
                    Dpi,
                    DATEDIFF(YEAR, FechaNacimiento, GETDATE())
                      - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, FechaNacimiento, GETDATE()),
                                          FechaNacimiento) > GETDATE()
                             THEN 1 ELSE 0 END AS Edad,
                    Psicopatologias,
                    MedicamentosCajon,
                    FechaIngreso
            FROM Residentes WHERE Id = @ResidenteId;";

        const string sqlConsultas = @"
            SELECT  c.Id                            AS CitaId,
                    c.BloqueHorario                 AS Fecha,
                    esp.Nombre                      AS Especialidad,
                    u.NombreCompleto                AS Especialista,
                    med.NombreCompleto              AS MedicoRemitente,
                    rem.Sintomatologia,
                    c.Diagnostico,
                    CASE c.Estado
                         WHEN 0 THEN 'Sin consulta'
                         WHEN 1 THEN 'En consulta'
                         WHEN 2 THEN 'Esperando anexos'
                         WHEN 3 THEN 'Consulta cerrada'
                         ELSE 'Costos aplicados' END AS Estado,
                    ISNULL(lab.Lista, '')           AS Examenes,
                    ISNULL(far.Lista, '')           AS Medicamentos
            FROM CitasFundacion c
            INNER JOIN Remisiones     rem ON rem.Id = c.RemisionId
            INNER JOIN Especialidades esp ON esp.Id = rem.EspecialidadId
            INNER JOIN Usuarios         u ON u.Id   = c.EspecialistaId
            INNER JOIN Usuarios       med ON med.Id = rem.MedicoGeneralId
            OUTER APPLY (
                SELECT STRING_AGG(o.TipoExamen
                       + CASE WHEN o.Resultado IS NULL THEN ' (pendiente)' ELSE '' END, '; ') AS Lista
                FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) lab
            OUTER APPLY (
                SELECT STRING_AGG(d.Medicamento + ' x' + CAST(d.Cantidad AS VARCHAR(10))
                       + CASE WHEN d.Estado = 0 THEN ' (sin entregar)' ELSE '' END, '; ') AS Lista
                FROM DespachosFarmacia d WHERE d.CitaId = c.Id) far
            WHERE rem.ResidenteId = @ResidenteId
              AND (@Desde IS NULL OR c.BloqueHorario >= @Desde)
              AND (@Hasta IS NULL OR c.BloqueHorario < DATEADD(DAY, 1, @Hasta))
            ORDER BY c.BloqueHorario DESC, c.Id DESC;";

        using var conexion = _context.CreateConnection();

        var residente = await conexion.QuerySingleOrDefaultAsync(sqlResidente, new { ResidenteId = residenteId });
        if (residente is null) return null;

        var consultas = await conexion.QueryAsync<RenglonHistorialDto>(
            sqlConsultas, new { ResidenteId = residenteId, Desde = desde, Hasta = hasta });

        return new ReporteHistorialDto
        {
            Encabezado = new EncabezadoReporte
            {
                Titulo = "Historial clínico",
                Desde = desde,
                Hasta = hasta,
                Residente = (string)residente.Residente
            },
            Residente = (string)residente.Residente,
            Dpi = residente.Dpi,
            Edad = (int)residente.Edad,
            Psicopatologias = residente.Psicopatologias,
            MedicamentosCajon = residente.MedicamentosCajon,
            FechaIngreso = (DateTime)residente.FechaIngreso,
            Consultas = consultas.ToList()
        };
    }

    // ==================== 2. COSTOS POR CITA ====================

    public async Task<ReporteCostosCitaDto> CostosPorCitaAsync(DateTime? desde, DateTime? hasta)
    {
        // OUTER APPLY y no JOIN: una cita puede no tener laboratorio o
        // no tener farmacia, y con INNER JOIN desapareceria del reporte
        // justo la cita mas barata.
        const string sql = @"
            SELECT  c.Id                            AS CitaId,
                    c.BloqueHorario                 AS Fecha,
                    r.Nombres + ' ' + r.Apellidos   AS Residente,
                    esp.Nombre                      AS Especialidad,
                    c.CostoConsulta,
                    ISNULL(lab.Total, 0)            AS CostoLaboratorio,
                    ISNULL(far.Total, 0)            AS CostoFarmacia,
                    c.CostoConsulta + ISNULL(lab.Total, 0) + ISNULL(far.Total, 0) AS Total,
                    CASE c.Estado
                         WHEN 0 THEN 'Sin consulta'
                         WHEN 1 THEN 'En consulta'
                         WHEN 2 THEN 'Esperando anexos'
                         WHEN 3 THEN 'Consulta cerrada'
                         ELSE 'Costos aplicados' END AS Estado,
                    c.Liquidada
            FROM CitasFundacion c
            INNER JOIN Remisiones     rem ON rem.Id = c.RemisionId
            INNER JOIN Residentes       r ON r.Id   = rem.ResidenteId
            INNER JOIN Especialidades esp ON esp.Id = rem.EspecialidadId
            OUTER APPLY (SELECT SUM(o.CostoBruto) AS Total
                         FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) lab
            OUTER APPLY (SELECT SUM(d.CostoTotal) AS Total
                         FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) far
            WHERE (@Desde IS NULL OR c.BloqueHorario >= @Desde)
              AND (@Hasta IS NULL OR c.BloqueHorario < DATEADD(DAY, 1, @Hasta))
            ORDER BY c.BloqueHorario DESC, c.Id DESC;";

        using var conexion = _context.CreateConnection();
        var citas = (await conexion.QueryAsync<RenglonCostoCitaDto>(
            sql, new { Desde = desde, Hasta = hasta })).ToList();

        return new ReporteCostosCitaDto
        {
            Encabezado = new EncabezadoReporte
            {
                Titulo = "Costos por cita",
                Desde = desde,
                Hasta = hasta
            },
            Citas = citas,
            TotalConsultas = citas.Sum(c => c.CostoConsulta),
            TotalLaboratorio = citas.Sum(c => c.CostoLaboratorio),
            TotalFarmacia = citas.Sum(c => c.CostoFarmacia),
            TotalGeneral = citas.Sum(c => c.Total)
        };
    }

    // ==================== 3. INGRESOS Y EGRESOS ====================

    /// <summary>
    /// El libro del periodo: lo que entro y lo que salio.
    ///
    /// Los ingresos son de dos clases distintas y por eso se suman
    /// aparte: lo que cobra el asilo a las familias (los abonos sobre
    /// facturas) y lo que recibe por donacion. Mezclarlos daria un
    /// total correcto y una lectura inutil, porque no se sabria cuanto
    /// del sostenimiento depende de la caridad.
    /// </summary>
    public async Task<ReporteIngresosEgresosDto> IngresosEgresosAsync(DateTime? desde, DateTime? hasta)
    {
        const string sql = @"
            SELECT  CAST(p.FechaPago AS DATE)       AS Fecha,
                    'Ingreso'                       AS Clase,
                    'Cuota y servicios'             AS Concepto,
                    r.Nombres + ' ' + r.Apellidos
                      + ' · ' + p.MetodoPago        AS Detalle,
                    p.MontoRecibido                 AS Ingreso,
                    CAST(0 AS DECIMAL(10,2))        AS Egreso
            FROM Pagos p
            INNER JOIN Facturas   f ON f.Id = p.FacturaId
            INNER JOIN Residentes r ON r.Id = f.ResidenteId
            WHERE (@Desde IS NULL OR p.FechaPago >= @Desde)
              AND (@Hasta IS NULL OR p.FechaPago < DATEADD(DAY, 1, @Hasta))

            UNION ALL

            SELECT  d.Fecha,
                    'Ingreso',
                    'Donación',
                    d.NombreDonante
                      + CASE WHEN d.Descripcion IS NULL THEN ''
                             ELSE ' · ' + d.Descripcion END,
                    d.Monto,
                    0
            FROM Donaciones d
            WHERE (@Desde IS NULL OR d.Fecha >= @Desde)
              AND (@Hasta IS NULL OR d.Fecha <= @Hasta)

            UNION ALL

            SELECT  g.Fecha,
                    'Egreso',
                    g.Categoria,
                    g.Concepto,
                    0,
                    g.Monto
            FROM GastosOperativos g
            WHERE (@Desde IS NULL OR g.Fecha >= @Desde)
              AND (@Hasta IS NULL OR g.Fecha <= @Hasta)

            ORDER BY Fecha DESC;";

        using var conexion = _context.CreateConnection();
        var movimientos = (await conexion.QueryAsync<RenglonMovimientoDto>(
            sql, new { Desde = desde, Hasta = hasta })).ToList();

        decimal cuotas = movimientos.Where(m => m.Concepto == "Cuota y servicios").Sum(m => m.Ingreso);
        decimal donaciones = movimientos.Where(m => m.Concepto == "Donación").Sum(m => m.Ingreso);
        decimal egresos = movimientos.Sum(m => m.Egreso);

        return new ReporteIngresosEgresosDto
        {
            Encabezado = new EncabezadoReporte
            {
                Titulo = "Ingresos y egresos",
                Desde = desde,
                Hasta = hasta
            },
            Movimientos = movimientos,
            TotalCuotasYServicios = cuotas,
            TotalDonaciones = donaciones,
            TotalIngresos = cuotas + donaciones,
            TotalEgresos = egresos,
            Resultado = cuotas + donaciones - egresos
        };
    }

    // ==================== 4. PAGOS A LA FUNDACION ====================

    public async Task<ReportePagosFundacionDto> PagosALaFundacionAsync(DateTime? desde, DateTime? hasta)
    {
        // Lo adeudado sale de las facturas y no de los gastos: es la
        // suma de los servicios que la fundacion presto, ya descontado
        // el convenio. La cuota de estancia queda fuera a proposito,
        // porque es ingreso del asilo y no un servicio de la fundacion.
        const string sqlAdeudado = @"
            SELECT  ISNULL(SUM(f.TotalServicios), 0)    AS ServiciosFacturados,
                    ISNULL(SUM(f.DescuentoAplicado), 0) AS DescuentoConvenio
            FROM Facturas f
            WHERE (@Desde IS NULL OR f.Periodo >= @Desde)
              AND (@Hasta IS NULL OR f.Periodo <= @Hasta);";

        const string sqlPagos = @"
            SELECT Fecha, Concepto, Monto
            FROM GastosOperativos
            WHERE Categoria = 'PagoFundacion'
              AND (@Desde IS NULL OR Fecha >= @Desde)
              AND (@Hasta IS NULL OR Fecha <= @Hasta)
            ORDER BY Fecha DESC, Id DESC;";

        using var conexion = _context.CreateConnection();

        var totales = await conexion.QuerySingleAsync(sqlAdeudado, new { Desde = desde, Hasta = hasta });
        var pagos = (await conexion.QueryAsync<RenglonPagoFundacionDto>(
            sqlPagos, new { Desde = desde, Hasta = hasta })).ToList();

        decimal servicios = (decimal)totales.ServiciosFacturados;
        decimal descuento = (decimal)totales.DescuentoConvenio;
        decimal adeudado = servicios - descuento;
        decimal pagado = pagos.Sum(p => p.Monto);

        return new ReportePagosFundacionDto
        {
            Encabezado = new EncabezadoReporte
            {
                Titulo = "Pagos a la fundación",
                Desde = desde,
                Hasta = hasta
            },
            Pagos = pagos,
            ServiciosFacturados = servicios,
            DescuentoConvenio = descuento,
            AdeudadoALaFundacion = adeudado,
            TotalPagado = pagado,
            Saldo = adeudado - pagado
        };
    }

    // ==================== 5. EXAMENES E INSUMOS ====================

    public async Task<ReporteInsumosDto> ExamenesEInsumosAsync(
        int? residenteId, DateTime? desde, DateTime? hasta)
    {
        const string sql = @"
            SELECT  o.FechaSolicitud                AS Fecha,
                    r.Nombres + ' ' + r.Apellidos   AS Residente,
                    o.CitaId,
                    'Examen'                        AS Clase,
                    o.TipoExamen                    AS Detalle,
                    1                               AS Cantidad,
                    CASE o.Estado WHEN 0 THEN 'Pendiente' ELSE 'Completado' END AS Situacion,
                    o.CostoBruto                    AS Costo
            FROM OrdenesLaboratorio o
            INNER JOIN CitasFundacion c ON c.Id   = o.CitaId
            INNER JOIN Remisiones   rem ON rem.Id = c.RemisionId
            INNER JOIN Residentes     r ON r.Id   = rem.ResidenteId
            WHERE (@ResidenteId IS NULL OR rem.ResidenteId = @ResidenteId)
              AND (@Desde IS NULL OR o.FechaSolicitud >= @Desde)
              AND (@Hasta IS NULL OR o.FechaSolicitud < DATEADD(DAY, 1, @Hasta))

            UNION ALL

            SELECT  d.FechaReceta,
                    r.Nombres + ' ' + r.Apellidos,
                    d.CitaId,
                    'Medicamento',
                    d.Medicamento
                      + CASE WHEN d.TiempoAplicacion IS NULL THEN ''
                             ELSE ' · ' + d.TiempoAplicacion END,
                    d.Cantidad,
                    CASE d.Estado WHEN 0 THEN 'Por entregar' ELSE 'Entregado' END,
                    d.CostoTotal
            FROM DespachosFarmacia d
            INNER JOIN CitasFundacion c ON c.Id   = d.CitaId
            INNER JOIN Remisiones   rem ON rem.Id = c.RemisionId
            INNER JOIN Residentes     r ON r.Id   = rem.ResidenteId
            WHERE (@ResidenteId IS NULL OR rem.ResidenteId = @ResidenteId)
              AND (@Desde IS NULL OR d.FechaReceta >= @Desde)
              AND (@Hasta IS NULL OR d.FechaReceta < DATEADD(DAY, 1, @Hasta))

            ORDER BY Fecha DESC;";

        using var conexion = _context.CreateConnection();
        var renglones = (await conexion.QueryAsync<RenglonInsumoDto>(
            sql, new { ResidenteId = residenteId, Desde = desde, Hasta = hasta })).ToList();

        string? nombre = null;
        if (residenteId is not null)
        {
            nombre = await conexion.ExecuteScalarAsync<string>(
                "SELECT Nombres + ' ' + Apellidos FROM Residentes WHERE Id = @Id;",
                new { Id = residenteId });
        }

        return new ReporteInsumosDto
        {
            Encabezado = new EncabezadoReporte
            {
                Titulo = "Exámenes e insumos aplicados",
                Desde = desde,
                Hasta = hasta,
                Residente = nombre
            },
            Renglones = renglones,
            TotalExamenes = renglones.Count(x => x.Clase == "Examen"),
            TotalMedicamentos = renglones.Count(x => x.Clase == "Medicamento"),
            CostoLaboratorio = renglones.Where(x => x.Clase == "Examen").Sum(x => x.Costo),
            CostoFarmacia = renglones.Where(x => x.Clase == "Medicamento").Sum(x => x.Costo),
            CostoTotal = renglones.Sum(x => x.Costo)
        };
    }
}
