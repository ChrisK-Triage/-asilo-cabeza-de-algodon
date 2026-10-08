namespace Asilo.Api.Models;

public enum EstadoRemision { Pendiente = 0, Programada = 1, Cancelada = 2 }

public class Remision
{
    public int Id { get; set; }
    public int ResidenteId { get; set; }
    public int MedicoGeneralId { get; set; }
    public string Sintomatologia { get; set; } = string.Empty;
    public string EspecialidadSolicitada { get; set; } = string.Empty;
    public string? EnfermeroAsignado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoRemision Estado { get; set; }

    // Se llena con multi-mapping cuando la consulta trae el JOIN
    public Residente? Residente { get; set; }
}
