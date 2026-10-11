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

public class DespachoCreateDto
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

    [Range(0, 999999, ErrorMessage = "El costo unitario no puede ser negativo.")]
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
    public DateTime FechaDespacho { get; set; }
}

// Resumen de anexos de una cita, para saber cuanto lleva acumulado.
public class ResumenAnexosDto
{
    public int CitaId { get; set; }
    public int TotalOrdenes { get; set; }
    public int OrdenesPendientes { get; set; }
    public decimal CostoLaboratorio { get; set; }
    public int TotalDespachos { get; set; }
    public decimal CostoFarmacia { get; set; }
    public decimal CostoConsulta { get; set; }
    public decimal CostoTotalCita { get; set; }
}
