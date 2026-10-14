using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

// ---------------- Laboratorio ----------------

// El especialista crea la orden durante la consulta.
public class OrdenCreateDto
{
    [Required(ErrorMessage = "Debe indicar la cita.")]
    [Range(1, int.MaxValue)]
    public int CitaId { get; set; }

    [Required(ErrorMessage = "El tipo de examen es obligatorio.")]
    [MaxLength(150)]
    public string TipoExamen { get; set; } = string.Empty;
}

// El tecnico carga el resultado y el costo. Separado del create
// porque son dos roles distintos en dos momentos distintos.
public class ResultadoDto
{
    [Required(ErrorMessage = "El resultado es obligatorio.")]
    public string Resultado { get; set; } = string.Empty;

    [Range(0, 999999, ErrorMessage = "El costo no puede ser negativo.")]
    public decimal CostoBruto { get; set; }
}

public class OrdenDto
{
    public int Id { get; set; }
    public int CitaId { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public string EspecialistaNombre { get; set; } = string.Empty;
    public string TipoExamen { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public decimal CostoBruto { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaResultado { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int DiasPendiente { get; set; }
}

// ---------------- Farmacia ----------------

// El especialista receta durante la consulta. Sin costo: el precio lo
// pone farmacia al entregar, igual que el tecnico de laboratorio pone
// el costo del examen al cargar el resultado.
public class RecetaCreateDto
{
    [Required(ErrorMessage = "Debe indicar la cita.")]
    [Range(1, int.MaxValue)]
    public int CitaId { get; set; }

    [Required(ErrorMessage = "El medicamento es obligatorio.")]
    [MaxLength(150)]
    public string Medicamento { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public int Cantidad { get; set; }

    [MaxLength(100)]
    public string? TiempoAplicacion { get; set; }
}

// Farmacia entrega y fija el precio.
public class EntregaDto
{
    // Mayor a cero y no "no negativo": un medicamento entregado en cero
    // es un cargo que se pierde sin que nada lo advierta. Lo respalda
    // tambien CK_Despachos_CostoEntregado en la base.
    [Range(0.01, 999999, ErrorMessage = "El costo unitario debe ser mayor a cero.")]
    public decimal CostoUnitario { get; set; }
}

public class DespachoDto
{
    public int Id { get; set; }
    public int CitaId { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public string Medicamento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string? TiempoAplicacion { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaReceta { get; set; }
    public DateTime? FechaDespacho { get; set; }
    public int DiasPendiente { get; set; }
}

/// <summary>
/// Lo que necesito saber de una cita para decidir si todavia admite un
/// examen o un medicamento. Lo traigo de un solo viaje en lugar de
/// preguntar tres veces lo mismo a la base.
/// </summary>
public class EstadoAnexosDto
{
    public int CitaId { get; set; }
    public byte Estado { get; set; }
    public bool TieneDiagnostico { get; set; }
    public bool RequiereLaboratorio { get; set; }
    public bool RequiereFarmacia { get; set; }
    public bool Liquidada { get; set; }
}

// Resumen de anexos de una cita, para saber cuanto lleva acumulado.
public class ResumenAnexosDto
{
    public int CitaId { get; set; }
    public int TotalOrdenes { get; set; }
    public int OrdenesPendientes { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public int TotalDespachos { get; set; }
    public int DespachosPendientes { get; set; }
    public decimal CostoFarmacia { get; set; }
    public decimal CostoConsulta { get; set; }
    public decimal CostoTotalCita { get; set; }
}
