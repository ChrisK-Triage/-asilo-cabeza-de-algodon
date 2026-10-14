namespace Asilo.Api.Models;

/// <summary>
/// Recetado: el especialista indico el medicamento, la cantidad y cada
/// cuanto se aplica. Todavia no hay costo ni entrega.
///
/// Despachado: farmacia lo entrego y fijo el precio unitario. Es el
/// momento en que el cargo existe para la factura.
/// </summary>
public enum EstadoDespacho
{
    Recetado = 0,
    Despachado = 1
}

public class DespachoFarmacia
{
    public int Id { get; set; }
    public int CitaId { get; set; }
    public string Medicamento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string? TiempoAplicacion { get; set; }

    // Vale 0 mientras la receta no se entrega: lo recetado no se cobra.
    public decimal CostoUnitario { get; set; }

    // Columna calculada en SQL Server (Cantidad * CostoUnitario PERSISTED).
    // La leo pero nunca la escribo: el motor la mantiene.
    public decimal CostoTotal { get; set; }

    public EstadoDespacho Estado { get; set; } = EstadoDespacho.Recetado;

    public DateTime FechaReceta { get; set; }

    // Nulo mientras no se entregue. CK_Despachos_EstadoFecha obliga a que
    // esta fecha y el estado digan lo mismo.
    public DateTime? FechaDespacho { get; set; }
}
