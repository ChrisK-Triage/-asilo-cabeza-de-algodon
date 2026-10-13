namespace Asilo.Api.Models;

public class Residente
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Dpi { get; set; }
    public DateTime FechaNacimiento { get; set; }

    // Lo que paga el familiar por la estancia. Es ingreso del asilo y
    // queda fuera del descuento del convenio, que solo cubre los
    // servicios de la fundacion.
    public decimal CuotaMensual { get; set; }

    public string? Psicopatologias { get; set; }
    public string? MedicamentosCajon { get; set; }
    public DateTime FechaIngreso { get; set; }
    public bool Activo { get; set; }
}
