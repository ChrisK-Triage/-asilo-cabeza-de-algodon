namespace Asilo.Api.DTOs;

/// <summary>
/// Cabecera comun de todo reporte: que se pidio y cuando se genero.
///
/// Va en la respuesta y no solo en la pantalla porque un reporte
/// impreso sin el rango que lo produjo no se puede verificar despues:
/// no hay forma de saber si esas cifras corresponden a enero o al ano
/// completo.
/// </summary>
public class EncabezadoReporte
{
    public string Titulo { get; set; } = string.Empty;
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public DateTime Generado { get; set; } = DateTime.Now;
    public string? Residente { get; set; }
}

// ---------------- 1. Historial clinico ----------------

public class RenglonHistorialDto
{
    public int CitaId { get; set; }
    public DateTime Fecha { get; set; }
    public string Especialidad { get; set; } = string.Empty;
    public string Especialista { get; set; } = string.Empty;
    public string MedicoRemitente { get; set; } = string.Empty;
    public string Sintomatologia { get; set; } = string.Empty;
    public string? Diagnostico { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Examenes { get; set; } = string.Empty;
    public string Medicamentos { get; set; } = string.Empty;
}

public class ReporteHistorialDto
{
    public EncabezadoReporte Encabezado { get; set; } = new();
    public string Residente { get; set; } = string.Empty;
    public string? Dpi { get; set; }
    public int Edad { get; set; }
    public string? Psicopatologias { get; set; }
    public string? MedicamentosCajon { get; set; }
    public DateTime FechaIngreso { get; set; }
    public List<RenglonHistorialDto> Consultas { get; set; } = new();
}

// ---------------- 2. Costos por cita ----------------

public class RenglonCostoCitaDto
{
    public int CitaId { get; set; }
    public DateTime Fecha { get; set; }
    public string Residente { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public decimal CostoConsulta { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public decimal CostoFarmacia { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Liquidada { get; set; }
}

public class ReporteCostosCitaDto
{
    public EncabezadoReporte Encabezado { get; set; } = new();
    public List<RenglonCostoCitaDto> Citas { get; set; } = new();
    public decimal TotalConsultas { get; set; }
    public decimal TotalLaboratorio { get; set; }
    public decimal TotalFarmacia { get; set; }
    public decimal TotalGeneral { get; set; }
}

// ---------------- 3. Ingresos y egresos ----------------

public class RenglonMovimientoDto
{
    public DateTime Fecha { get; set; }
    public string Clase { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public decimal Ingreso { get; set; }
    public decimal Egreso { get; set; }
}

public class ReporteIngresosEgresosDto
{
    public EncabezadoReporte Encabezado { get; set; } = new();
    public List<RenglonMovimientoDto> Movimientos { get; set; } = new();

    public decimal TotalCuotasYServicios { get; set; }
    public decimal TotalDonaciones { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }

    /// <summary>Ingresos menos egresos. Negativo significa perdida en el periodo.</summary>
    public decimal Resultado { get; set; }
}

// ---------------- 4. Pagos a la fundacion ----------------

public class RenglonPagoFundacionDto
{
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>
/// Lo que el asilo le debe a la fundacion y lo que le ha pagado.
///
/// Lo adeudado no sale de los gastos sino de las facturas: es la suma
/// de los servicios clinicos que la fundacion presto, ya descontado el
/// convenio. La cuota de estancia no entra, porque esa es ingreso del
/// asilo y no un servicio de la fundacion.
/// </summary>
public class ReportePagosFundacionDto
{
    public EncabezadoReporte Encabezado { get; set; } = new();
    public List<RenglonPagoFundacionDto> Pagos { get; set; } = new();

    public decimal ServiciosFacturados { get; set; }
    public decimal DescuentoConvenio { get; set; }
    public decimal AdeudadoALaFundacion { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal Saldo { get; set; }
}

// ---------------- 5. Examenes e insumos por residente ----------------

public class RenglonInsumoDto
{
    public DateTime Fecha { get; set; }
    public string Residente { get; set; } = string.Empty;
    public int CitaId { get; set; }
    public string Clase { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Situacion { get; set; } = string.Empty;
    public decimal Costo { get; set; }
}

public class ReporteInsumosDto
{
    public EncabezadoReporte Encabezado { get; set; } = new();
    public List<RenglonInsumoDto> Renglones { get; set; } = new();
    public int TotalExamenes { get; set; }
    public int TotalMedicamentos { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public decimal CostoFarmacia { get; set; }
    public decimal CostoTotal { get; set; }
}
