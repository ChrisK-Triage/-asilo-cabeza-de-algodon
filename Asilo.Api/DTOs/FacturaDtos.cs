using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

// ---------------- Calculo del periodo ----------------

/// <summary>
/// Una cita incluida en el calculo, con lo que aporta al total. La
/// devuelvo detallada para que el estado de cuenta pueda mostrar de
/// donde sale cada quetzal: un total sin desglose no se puede
/// verificar ni explicarle al familiar que lo paga.
/// </summary>
public class RenglonCitaDto
{
    public int CitaId { get; set; }
    public DateTime BloqueHorario { get; set; }
    public string Especialidad { get; set; } = string.Empty;
    public string EspecialistaNombre { get; set; } = string.Empty;
    public decimal CostoConsulta { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public decimal CostoFarmacia { get; set; }
    public decimal Subtotal => CostoConsulta + CostoLaboratorio + CostoFarmacia;
}

/// <summary>
/// El calculo de un periodo antes de emitir la factura.
///
/// La regla del convenio es la razon de ser de este DTO: el descuento
/// aplica sobre los servicios clinicos que presta la fundacion
/// —consulta, laboratorio y farmacia— y nunca sobre la cuota mensual
/// de estancia, que es un cobro del asilo. Por eso los dos conceptos
/// viajan separados y no como un unico total.
/// </summary>
public class CalculoPeriodoDto
{
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public int Anio { get; set; }
    public int Mes { get; set; }

    public List<RenglonCitaDto> Citas { get; set; } = new();

    public decimal CostoConsultas { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public decimal CostoFarmacia { get; set; }

    /// <summary>Base sobre la que se calcula el descuento.</summary>
    public decimal TotalServicios { get; set; }

    public int? ConvenioId { get; set; }
    public string? ConvenioDescripcion { get; set; }
    public decimal PorcentajeDescuento { get; set; }
    public decimal DescuentoAplicado { get; set; }

    /// <summary>Cobro del asilo. Queda fuera del descuento.</summary>
    public decimal CuotaMensual { get; set; }

    public decimal TotalBruto { get; set; }
    public decimal TotalNeto { get; set; }

    /// <summary>
    /// La factura que ya cobro este periodo, si existe. Cuando trae
    /// valor, lo que se esta calculando es una complementaria.
    /// </summary>
    public int? FacturaExistenteId { get; set; }

    /// <summary>
    /// Cierto cuando el periodo ya tiene factura y lo que queda por
    /// cobrar son cargos que llegaron despues del cierre.
    ///
    /// Una complementaria lleva CuotaMensual en cero: la estancia se
    /// cobro en la primera factura del mes y volver a cobrarla seria
    /// duplicarla. El indice UX_Facturas_CuotaUnicaPorPeriodo respalda
    /// esa regla desde la base, donde una condicion de carrera entre
    /// dos emisiones simultaneas no la puede esquivar.
    /// </summary>
    public bool EsComplementaria => FacturaExistenteId is not null;

    /// <summary>
    /// En la factura del periodo basta la cuota para que haya algo que
    /// cobrar. En una complementaria la cuota es cero, asi que lo unico
    /// que la justifica son las citas cerradas despues del corte.
    /// </summary>
    public bool HayQueFacturar => Citas.Count > 0 || CuotaMensual > 0;
}

// ---------------- Emision ----------------

public class EmitirFacturaDto
{
    [Required(ErrorMessage = "Debe indicar el residente.")]
    [Range(1, int.MaxValue)]
    public int ResidenteId { get; set; }

    [Required(ErrorMessage = "Debe indicar el anio del periodo.")]
    [Range(2020, 2100)]
    public int Anio { get; set; }

    [Required(ErrorMessage = "Debe indicar el mes del periodo.")]
    [Range(1, 12, ErrorMessage = "El mes debe estar entre 1 y 12.")]
    public int Mes { get; set; }
}

public class FacturaDto
{
    public int Id { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public int ConvenioId { get; set; }
    public string ConvenioDescripcion { get; set; } = string.Empty;
    public DateTime Periodo { get; set; }

    public decimal TotalServicios { get; set; }
    public decimal DescuentoAplicado { get; set; }
    public decimal CuotaMensual { get; set; }
    public decimal TotalBruto { get; set; }
    public decimal TotalNeto { get; set; }

    public DateTime FechaEmision { get; set; }
    public string Estado { get; set; } = string.Empty;

    /// <summary>Suma de los abonos recibidos.</summary>
    public decimal TotalPagado { get; set; }

    /// <summary>Lo que falta por cobrar. Es el dato que verifica CP-10.</summary>
    public decimal Saldo { get; set; }

    public int CitasIncluidas { get; set; }
}

// ---------------- Pagos ----------------

public class PagoCreateDto
{
    [Required(ErrorMessage = "Debe indicar el monto recibido.")]
    [Range(0.01, 9999999, ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal MontoRecibido { get; set; }

    [Required(ErrorMessage = "Debe indicar el metodo de pago.")]
    public string MetodoPago { get; set; } = string.Empty;

    public int? RegistradoPor { get; set; }
}

public class PagoDto
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public decimal MontoRecibido { get; set; }
    public DateTime FechaPago { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public string? RegistradoPorNombre { get; set; }
}

// ---------------- Estado de cuenta ----------------

/// <summary>
/// RF-10: lo que el residente debe en total y el detalle de sus
/// facturas. Es la vista que necesita el administrador para saber a
/// quien hay que cobrarle.
/// </summary>
public class EstadoCuentaDto
{
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public decimal TotalFacturado { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public List<FacturaDto> Facturas { get; set; } = new();
}
