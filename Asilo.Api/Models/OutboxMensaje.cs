namespace Asilo.Api.Models;

/// <summary>
/// Un aviso pendiente de entregar al familiar (RF-08).
///
/// El patron se llama Outbox y resuelve un problema concreto: la cita se
/// guarda en SQL Server y el correo se manda por otro medio. Son dos
/// sistemas distintos y no hay transaccion que los abarque a los dos.
///
/// Si se mandara el correo dentro de la operacion, un fallo del envio
/// tumbaria la cita; y si se mandara despues, un corte entre una cosa y
/// la otra dejaria la cita guardada sin que nadie avise. Lo que se hace
/// aqui es escribir el aviso en una tabla, en la misma transaccion que
/// la cita: o se guardan los dos o ninguno. Un proceso aparte los va
/// entregando despues, con reintentos.
/// </summary>
public class OutboxMensaje
{
    public long Id { get; set; }
    public string TipoEvento { get; set; } = string.Empty;

    /// <summary>
    /// El contenido del aviso, en JSON.
    ///
    /// Es una fotografia de lo que era cierto cuando ocurrio el evento,
    /// no una referencia a la fila. Si guardara solo el Id de la cita, el
    /// despachador tendria que volver a leerla al entregar, y para
    /// entonces el horario pudo haber cambiado: se enviaria un aviso que
    /// no corresponde al hecho que lo origino.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    public bool Procesado { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaProcesado { get; set; }
}

/// <summary>
/// Los eventos que generan aviso. Son los tres momentos en que el
/// enunciado pide mantener informada a la familia: cuando se le programa
/// una consulta al residente, cuando se emite el cobro del mes y cuando
/// se recibe un abono.
/// </summary>
public static class TipoEventoOutbox
{
    public const string CitaAsignada = "CitaAsignada";
    public const string FacturaEmitida = "FacturaEmitida";
    public const string PagoRegistrado = "PagoRegistrado";

    public static string Etiqueta(string tipo) => tipo switch
    {
        CitaAsignada => "Cita programada",
        FacturaEmitida => "Estado de cuenta del mes",
        PagoRegistrado => "Confirmación de pago",
        _ => tipo
    };
}
