namespace Asilo.Api.Models;

public class Familiar
{
    public int Id { get; set; }
    public int ResidenteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Nullable porque la columna admite NULL en la tabla
    public string? Telefono { get; set; }
}
