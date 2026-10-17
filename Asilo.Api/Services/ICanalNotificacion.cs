using Asilo.Api.Models;

namespace Asilo.Api.Services;

/// <summary>
/// Por donde sale el aviso.
///
/// Existe como interfaz y no como una llamada directa a SMTP porque el
/// canal es lo unico que cambia al pasar a produccion: el Outbox, los
/// reintentos y la transaccion siguen igual. Hoy la implementacion
/// escribe en el registro; manana se cambia por una que mande correo
/// sin tocar nada mas.
/// </summary>
public interface ICanalNotificacion
{
    /// <summary>
    /// Entrega el aviso. Lanzar una excepcion significa "no se pudo":
    /// el despachador suma un intento y lo deja pendiente.
    /// </summary>
    Task EnviarAsync(OutboxMensaje mensaje, CancellationToken cancelacion);
}
