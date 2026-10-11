namespace Asilo.Api.Models;

public enum EstadoOrden { Pendiente = 0, Completada = 1 }

public class OrdenLaboratorio
{
    public int Id { get; set; }
    public int CitaId { get; set; }
    public string TipoExamen { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public decimal CostoBruto { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaResultado { get; set; }
    public EstadoOrden Estado { get; set; }
}
