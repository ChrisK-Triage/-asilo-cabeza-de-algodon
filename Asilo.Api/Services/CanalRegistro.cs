using System.Text.Json;
using Asilo.Api.Models;

namespace Asilo.Api.Services;

/// <summary>
/// Canal de desarrollo: en lugar de mandar el correo, lo escribe en el
/// registro de la aplicacion.
///
/// No es un sustituto provisional mal hecho sino lo que corresponde en
/// esta fase. Enlazar un servidor SMTP real al proyecto significaria
/// mandar correo de verdad a direcciones de prueba cada vez que alguien
/// ejecuta el sistema. Lo que hay que demostrar es que el aviso se
/// genera, se encola con su contenido y se entrega una sola vez; eso se
/// comprueba igual leyendo la consola y la bandeja.
/// </summary>
public class CanalRegistro : ICanalNotificacion
{
    private readonly ILogger<CanalRegistro> _log;

    public CanalRegistro(ILogger<CanalRegistro> log) => _log = log;

    public Task EnviarAsync(OutboxMensaje mensaje, CancellationToken cancelacion)
    {
        string destinatarios = LeerDestinatarios(mensaje.Payload);

        // Sin destinatario no hay nada que entregar, y marcarlo como
        // entregado seria mentir. Lo dejo fallar para que caiga en la
        // bandeja con su motivo: el residente no tiene contacto
        // registrado, y eso lo arregla administracion, no el sistema.
        if (destinatarios.Length == 0)
        {
            throw new InvalidOperationException(
                "El residente no tiene contactos familiares registrados. " +
                "No hay a quien enviarle el aviso.");
        }

        _log.LogInformation(
            "[Notificacion] {Tipo} -> {Destinatarios}\n{Payload}",
            TipoEventoOutbox.Etiqueta(mensaje.TipoEvento),
            destinatarios,
            mensaje.Payload);

        return Task.CompletedTask;
    }

    private static string LeerDestinatarios(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);

            if (!doc.RootElement.TryGetProperty("destinatarios", out var lista)
                || lista.ValueKind != JsonValueKind.Array)
                return string.Empty;

            return string.Join(", ", lista.EnumerateArray()
                .Select(d => d.TryGetProperty("email", out var e) ? e.GetString() : null)
                .Where(c => !string.IsNullOrWhiteSpace(c)));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
