using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Asilo.Api.Repositories;

public class FacturaRepository : IFacturaRepository
{
    private readonly DapperContext _context;

    public FacturaRepository(DapperContext context) => _context = context;

    // ==================== CALCULO DEL PERIODO ====================

    /// <summary>
    /// Reune las citas facturables del periodo y aplica la regla del
    /// convenio.
    ///
    /// Que cita entra: la que pertenece al residente, cae en el mes
    /// pedido, tiene la bitacora cerrada y no se ha liquidado. Las tres
    /// condiciones importan. Una cita que todavia espera un examen
    /// tiene el costo incompleto, y facturarla significaria cobrar de
    /// menos y no poder cobrar la diferencia despues, porque al
    /// liquidarla deja de admitir cargos.
    /// </summary>
    public async Task<CalculoPeriodoDto?> CalcularPeriodoAsync(int residenteId, int anio, int mes)
    {
        const string sqlResidente = @"
            SELECT Id, Nombres + ' ' + Apellidos AS Nombre, CuotaMensual
            FROM Residentes WHERE Id = @ResidenteId;";

        const string sqlConvenio = @"
            SELECT TOP 1 Id, Descripcion, PorcentajeDescuento
            FROM ConveniosFundacion WHERE Vigente = 1;";

        // Un renglon por cita, con sus tres componentes separados.
        // OUTER APPLY y no JOIN: una cita puede no tener laboratorio,
        // o no tener farmacia, y con un INNER JOIN desapareceria.
        const string sqlCitas = @"
            SELECT  c.Id                            AS CitaId,
                    c.BloqueHorario,
                    esp.Nombre                      AS Especialidad,
                    u.NombreCompleto                AS EspecialistaNombre,
                    c.CostoConsulta,
                    ISNULL(lab.Total, 0)            AS CostoLaboratorio,
                    ISNULL(far.Total, 0)            AS CostoFarmacia
            FROM CitasFundacion c
            INNER JOIN Remisiones     rem ON rem.Id = c.RemisionId
            INNER JOIN Especialidades esp ON esp.Id = rem.EspecialidadId
            INNER JOIN Usuarios       u   ON u.Id   = c.EspecialistaId
            OUTER APPLY (SELECT SUM(o.CostoBruto) AS Total
                         FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) lab
            OUTER APPLY (SELECT SUM(d.CostoTotal) AS Total
                         FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) far
            WHERE rem.ResidenteId = @ResidenteId
              AND YEAR(c.BloqueHorario)  = @Anio
              AND MONTH(c.BloqueHorario) = @Mes
              AND c.Estado   >= 3      -- bitacora cerrada o posterior
              AND c.Liquidada = 0
            ORDER BY c.BloqueHorario;";

        using var conexion = _context.CreateConnection();

        var residente = await conexion.QuerySingleOrDefaultAsync(sqlResidente, new { ResidenteId = residenteId });
        if (residente is null) return null;

        var convenio = await conexion.QuerySingleOrDefaultAsync(sqlConvenio);
        var citas = (await conexion.QueryAsync<RenglonCitaDto>(
            sqlCitas, new { ResidenteId = residenteId, Anio = anio, Mes = mes })).ToList();

        var calculo = new CalculoPeriodoDto
        {
            ResidenteId = residenteId,
            ResidenteNombre = (string)residente.Nombre,
            Anio = anio,
            Mes = mes,
            Citas = citas,
            CuotaMensual = (decimal)residente.CuotaMensual,
            ConvenioId = convenio is null ? null : (int)convenio.Id,
            ConvenioDescripcion = convenio?.Descripcion,
            PorcentajeDescuento = convenio is null ? 0m : (decimal)convenio.PorcentajeDescuento
        };

        calculo.CostoConsultas   = citas.Sum(c => c.CostoConsulta);
        calculo.CostoLaboratorio = citas.Sum(c => c.CostoLaboratorio);
        calculo.CostoFarmacia    = citas.Sum(c => c.CostoFarmacia);

        // La busqueda de factura previa va ANTES de aplicar el convenio,
        // no despues: si el periodo ya se facturo, la cuota de estancia
        // tiene que entrar en cero al calculo, y AplicarConvenio la usa.
        // Calcularla primero y corregirla despues dejaria TotalBruto y
        // TotalNeto con la cuota adentro.
        //
        // ORDER BY Id: me interesa la primera factura del mes, que es la
        // que cobro la estancia.
        calculo.FacturaExistenteId = await conexion.ExecuteScalarAsync<int?>(@"
            SELECT TOP 1 Id FROM Facturas
            WHERE ResidenteId = @ResidenteId
              AND YEAR(Periodo) = @Anio AND MONTH(Periodo) = @Mes
            ORDER BY Id;",
            new { ResidenteId = residenteId, Anio = anio, Mes = mes });

        // Complementaria: solo los servicios de las citas que se
        // cerraron despues del corte. La estancia ya se cobro.
        if (calculo.EsComplementaria) calculo.CuotaMensual = 0m;

        AplicarConvenio(calculo);

        return calculo;
    }

    /// <summary>
    /// La regla del convenio, en un solo lugar.
    ///
    /// El orden de las operaciones es lo unico que importa aqui, y es
    /// donde esta el error facil: el descuento se calcula sobre los
    /// servicios y la cuota se suma despues, ya descontada la base. Si
    /// se sumara la cuota primero y se descontara sobre el total, el
    /// asilo estaria regalando parte de su propio ingreso, que no es lo
    /// que ofrece la fundacion.
    ///
    /// El enunciado dice que la fundacion "brinda precios especiales por
    /// consulta medica, por laboratorio y por farmacia": enumera tres
    /// conceptos y la estancia no es uno de ellos.
    /// </summary>
    private static void AplicarConvenio(CalculoPeriodoDto c)
    {
        c.TotalServicios = c.CostoConsultas + c.CostoLaboratorio + c.CostoFarmacia;

        // Redondeo a dos decimales al mas cercano. decimal y no double:
        // en calculo monetario la coma flotante arrastra error.
        c.DescuentoAplicado = Math.Round(
            c.TotalServicios * c.PorcentajeDescuento / 100m, 2, MidpointRounding.AwayFromZero);

        c.TotalBruto = c.TotalServicios + c.CuotaMensual;
        c.TotalNeto  = (c.TotalServicios - c.DescuentoAplicado) + c.CuotaMensual;
    }

    // ==================== EMISION ====================

    /// <summary>
    /// Inserta la factura y marca sus citas, todo o nada.
    ///
    /// La transaccion no es opcional: si la factura se guardara y las
    /// citas quedaran sin marcar, el mes siguiente volverian a entrar en
    /// el calculo y se le cobrarian dos veces al familiar. Es
    /// exactamente el caso que el plan de pruebas verifica.
    /// </summary>
    public async Task<int> EmitirAsync(CalculoPeriodoDto calculo)
    {
        using var conexion = _context.CreateConnection();
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();

        try
        {
            const string sqlFactura = @"
                INSERT INTO Facturas (ResidenteId, ConvenioId, Periodo,
                                      TotalServicios, CuotaMensual,
                                      TotalBruto, DescuentoAplicado, TotalNeto, Estado)
                VALUES (@ResidenteId, @ConvenioId, @Periodo,
                        @TotalServicios, @CuotaMensual,
                        @TotalBruto, @DescuentoAplicado, @TotalNeto, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int facturaId = await conexion.QuerySingleAsync<int>(sqlFactura, new
            {
                calculo.ResidenteId,
                calculo.ConvenioId,
                Periodo = new DateTime(calculo.Anio, calculo.Mes, 1),
                calculo.TotalServicios,
                calculo.CuotaMensual,
                calculo.TotalBruto,
                calculo.DescuentoAplicado,
                calculo.TotalNeto
            }, transaccion);

            if (calculo.Citas.Count > 0)
            {
                // Estado 4 = CostosCalculados. Es la transicion que el
                // diagrama de estados declaraba y que hasta ahora no
                // ejecutaba nadie, porque este modulo no existia.
                const string sqlCitas = @"
                    UPDATE CitasFundacion
                    SET Liquidada = 1,
                        FacturaId = @FacturaId,
                        Estado    = 4
                    WHERE Id IN @Ids AND Liquidada = 0;";

                await conexion.ExecuteAsync(sqlCitas, new
                {
                    FacturaId = facturaId,
                    Ids = calculo.Citas.Select(c => c.CitaId).ToArray()
                }, transaccion);
            }

            transaccion.Commit();
            return facturaId;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627
                                   && ex.Message.Contains("UX_Facturas_CuotaUnicaPorPeriodo"))
        {
            // Dos emisiones simultaneas del mismo periodo: las dos leyeron
            // que no habia factura y las dos intentaron cobrar la estancia.
            // El indice filtrado deja pasar una sola. Traduzco el error de
            // SQL Server a algo que el controlador pueda convertir en 409,
            // porque el dato esta bien y lo que fallo es la carrera.
            transaccion.Rollback();
            throw new InvalidOperationException(
                "Otra emision acaba de facturar este periodo. Hay que volver a calcularlo: " +
                "lo que quede pendiente se cobra en una factura complementaria.", ex);
        }
        catch
        {
            transaccion.Rollback();
            throw;
        }
    }

    // ==================== CONSULTA ====================

    // El saldo sale de restar los abonos al neto. Lo calculo en la
    // consulta y no en C# para que siempre sea el mismo numero,
    // venga de donde venga la peticion.
    private const string SqlFacturas = @"
        SELECT  f.Id,
                f.ResidenteId,
                r.Nombres + ' ' + r.Apellidos AS ResidenteNombre,
                f.ConvenioId,
                cv.Descripcion                AS ConvenioDescripcion,
                f.Periodo,
                f.TotalServicios,
                f.DescuentoAplicado,
                f.CuotaMensual,
                f.TotalBruto,
                f.TotalNeto,
                f.FechaEmision,
                CASE f.Estado
                     WHEN 0 THEN 'Emitida'
                     WHEN 1 THEN 'Saldo pendiente'
                     ELSE 'Liquidada' END     AS Estado,
                ISNULL(pg.Total, 0)           AS TotalPagado,
                f.TotalNeto - ISNULL(pg.Total, 0) AS Saldo,
                (SELECT COUNT(*) FROM CitasFundacion c WHERE c.FacturaId = f.Id) AS CitasIncluidas
        FROM Facturas f
        INNER JOIN Residentes          r  ON r.Id  = f.ResidenteId
        INNER JOIN ConveniosFundacion  cv ON cv.Id = f.ConvenioId
        OUTER APPLY (SELECT SUM(p.MontoRecibido) AS Total
                     FROM Pagos p WHERE p.FacturaId = f.Id) pg";

    public async Task<FacturaDto?> ObtenerPorIdAsync(int id)
    {
        string sql = SqlFacturas + " WHERE f.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<FacturaDto>(sql, new { Id = id });
    }

    public async Task<IEnumerable<FacturaDto>> ObtenerPorResidenteAsync(int residenteId)
    {
        string sql = SqlFacturas + " WHERE f.ResidenteId = @ResidenteId ORDER BY f.Periodo DESC;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<FacturaDto>(sql, new { ResidenteId = residenteId });
    }

    public async Task<EstadoCuentaDto?> ObtenerEstadoCuentaAsync(int residenteId)
    {
        const string sqlResidente = @"
            SELECT Id, Nombres + ' ' + Apellidos AS Nombre
            FROM Residentes WHERE Id = @ResidenteId;";

        using var conexion = _context.CreateConnection();

        var residente = await conexion.QuerySingleOrDefaultAsync(sqlResidente, new { ResidenteId = residenteId });
        if (residente is null) return null;

        var facturas = (await ObtenerPorResidenteAsync(residenteId)).ToList();

        return new EstadoCuentaDto
        {
            ResidenteId = residenteId,
            ResidenteNombre = (string)residente.Nombre,
            Facturas = facturas,
            TotalFacturado = facturas.Sum(f => f.TotalNeto),
            TotalPagado = facturas.Sum(f => f.TotalPagado),
            SaldoPendiente = facturas.Sum(f => f.Saldo)
        };
    }

    // ==================== PAGOS ====================

    public async Task<IEnumerable<PagoDto>> ObtenerPagosAsync(int facturaId)
    {
        const string sql = @"
            SELECT  p.Id, p.FacturaId, p.MontoRecibido, p.FechaPago, p.MetodoPago,
                    u.NombreCompleto AS RegistradoPorNombre
            FROM Pagos p
            LEFT JOIN Usuarios u ON u.Id = p.RegistradoPor
            WHERE p.FacturaId = @FacturaId
            ORDER BY p.FechaPago;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<PagoDto>(sql, new { FacturaId = facturaId });
    }

    /// <summary>
    /// Registra el abono y recalcula el estado de la factura en la misma
    /// transaccion.
    ///
    /// El estado no se guarda como un dato aparte que haya que mantener
    /// sincronizado: se deduce de la suma de los abonos contra el neto,
    /// dentro de la misma sentencia que acaba de insertar el pago. Asi
    /// no puede quedar una factura cobrada por completo que siga
    /// diciendo que tiene saldo.
    /// </summary>
    public async Task<int> RegistrarPagoAsync(int facturaId, decimal monto, string metodo, int? registradoPor)
    {
        using var conexion = _context.CreateConnection();
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();

        try
        {
            const string sqlPago = @"
                INSERT INTO Pagos (FacturaId, MontoRecibido, MetodoPago, RegistradoPor)
                VALUES (@FacturaId, @Monto, @Metodo, @RegistradoPor);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int pagoId = await conexion.QuerySingleAsync<int>(sqlPago, new
            {
                FacturaId = facturaId,
                Monto = monto,
                Metodo = metodo,
                RegistradoPor = registradoPor
            }, transaccion);

            // 2 = Liquidada cuando los abonos alcanzan el neto,
            // 1 = SaldoPendiente mientras falte.
            const string sqlEstado = @"
                UPDATE f
                SET Estado = CASE WHEN ISNULL(pg.Total, 0) >= f.TotalNeto THEN 2 ELSE 1 END
                FROM Facturas f
                OUTER APPLY (SELECT SUM(p.MontoRecibido) AS Total
                             FROM Pagos p WHERE p.FacturaId = f.Id) pg
                WHERE f.Id = @FacturaId;";

            await conexion.ExecuteAsync(sqlEstado, new { FacturaId = facturaId }, transaccion);

            transaccion.Commit();
            return pagoId;
        }
        catch
        {
            transaccion.Rollback();
            throw;
        }
    }
}
