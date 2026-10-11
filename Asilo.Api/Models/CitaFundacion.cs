namespace Asilo.Api.Models;

public enum EstadoCita
{
    Programada = 0,
    EnEvaluacion = 1,
    EsperandoAnexos = 2,
    BitacoraCompletada = 3,
    CostosCalculados = 4
}

public class CitaFundacion
{
    public int Id { get; set; }
    public int RemisionId { get; set; }
    public int EspecialistaId { get; set; }
    public DateTime BloqueHorario { get; set; }
    public string? Diagnostico { get; set; }
    public decimal CostoConsulta { get; set; }
    public EstadoCita Estado { get; set; }
    public bool Liquidada { get; set; }

    // Que indico el especialista en la consulta. Lo guardo porque es el
    // unico criterio para saber cuando la bitacora quedo completa: sin
    // esto la cita no sabe que esta esperando.
    public bool RequiereLaboratorio { get; set; }
    public bool RequiereFarmacia { get; set; }
}
