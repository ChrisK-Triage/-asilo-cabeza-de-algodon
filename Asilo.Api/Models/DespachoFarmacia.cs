namespace Asilo.Api.Models;

public class DespachoFarmacia
{
    public int Id { get; set; }
    public int CitaId { get; set; }
    public string Medicamento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string? TiempoAplicacion { get; set; }
    public decimal CostoUnitario { get; set; }

    // Columna calculada en SQL Server (Cantidad * CostoUnitario PERSISTED).
    // La leo pero nunca la escribo: el motor la mantiene.
    public decimal CostoTotal { get; set; }

    public DateTime FechaDespacho { get; set; }
}
