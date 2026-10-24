using System.Globalization;
using System.Text.Json;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Asilo.Api.Services;

/// <summary>
/// El canal de produccion: manda el aviso por correo.
///
/// Es la pieza que CanalRegistro venia ocupando. Como el despachador
/// depende de ICanalNotificacion y no de una clase concreta, cambiar de
/// uno a otro es una linea en Program.cs: el Outbox, la transaccion, los
/// reintentos y el tope siguen funcionando igual.
///
/// Uso MailKit y no System.Net.Mail.SmtpClient porque esa clase esta
/// marcada como obsoleta por Microsoft para codigo nuevo: no maneja bien
/// STARTTLS moderno y la propia documentacion remite a MailKit.
/// </summary>
public class CanalCorreo : ICanalNotificacion
{
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<CanalCorreo> _log;

    private static readonly JsonSerializerOptions Formato = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CanalCorreo(IOptions<OpcionesCorreo> opciones, ILogger<CanalCorreo> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task EnviarAsync(OutboxMensaje mensaje, CancellationToken cancelacion)
    {
        var armado = Armar(mensaje);

        // Mismo criterio que tenia CanalRegistro: sin destinatario no hay
        // entrega, y marcarlo como entregado seria mentir. Lo dejo fallar
        // para que quede en la bandeja con su motivo. Esto lo arregla
        // administracion registrandole un contacto al residente, no el
        // sistema reintentando.
        if (armado.Destinatarios.Count == 0)
        {
            throw new InvalidOperationException(
                "El residente no tiene contactos familiares con correo registrado. " +
                "No hay a quien enviarle el aviso.");
        }

        var correo = new MimeMessage();
        correo.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.CorreoRemitente));

        if (!string.IsNullOrWhiteSpace(_opciones.RedirigirTodoA))
        {
            // Modo prueba: todo llega a una sola bandeja. Pongo en el
            // asunto a quien le habria llegado, porque si no, al revisar
            // la bandeja no hay forma de saber si el aviso se dirigio
            // bien.
            correo.To.Add(MailboxAddress.Parse(_opciones.RedirigirTodoA));
            correo.Subject = $"[Prueba -> {string.Join(", ", armado.Destinatarios.Select(d => d.Email))}] "
                           + armado.Asunto;
        }
        else
        {
            // Los familiares van en copia oculta y el visible es el
            // propio remitente. Son personas distintas que no tienen por
            // que ver el correo de las demas, y mandando uno solo el
            // envio es atomico: si falla, falla entero y el reintento no
            // le llega dos veces a quien ya lo recibio.
            correo.To.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.CorreoRemitente));

            foreach (var destino in armado.Destinatarios)
                correo.Bcc.Add(new MailboxAddress(destino.Nombre, destino.Email));

            correo.Subject = armado.Asunto;
        }

        correo.Body = new BodyBuilder
        {
            HtmlBody = armado.Cuerpo,
            TextBody = armado.CuerpoTexto
        }.ToMessageBody();

        using var cliente = new SmtpClient
        {
            Timeout = _opciones.TiempoLimiteSegundos * 1000
        };

        try
        {
            // StartTls y no SslOnConnect: el 587 de Gmail abre en claro y
            // sube a TLS con el comando STARTTLS. El 465 seria lo otro.
            await cliente.ConnectAsync(
                _opciones.Servidor, _opciones.Puerto, SecureSocketOptions.StartTls, cancelacion);

            await cliente.AuthenticateAsync(_opciones.Usuario, _opciones.Password, cancelacion);
            await cliente.SendAsync(correo, cancelacion);

            _log.LogInformation(
                "Aviso {Id} ({Tipo}) enviado a {Cuantos} destinatario(s).",
                mensaje.Id, TipoEventoOutbox.Etiqueta(mensaje.TipoEvento),
                armado.Destinatarios.Count);
        }
        finally
        {
            // Desconecto siempre, haya salido bien o mal. Una conexion
            // abierta que nadie cierra se queda consumiendo una de las
            // que el proveedor permite a la vez.
            if (cliente.IsConnected)
                await cliente.DisconnectAsync(true, CancellationToken.None);
        }
    }

    // ---------------- Armado del mensaje ----------------

    private record Armado(
        string Asunto,
        string Cuerpo,
        string CuerpoTexto,
        List<DestinatarioAviso> Destinatarios);

    private Armado Armar(OutboxMensaje mensaje) => mensaje.TipoEvento switch
    {
        TipoEventoOutbox.CitaAsignada    => ArmarCita(mensaje.Payload),
        TipoEventoOutbox.FacturaEmitida  => ArmarFactura(mensaje.Payload),
        TipoEventoOutbox.PagoRegistrado  => ArmarPago(mensaje.Payload),

        // Un tipo que no reconozco es un error de programacion, no un
        // fallo de entrega. Lanzo para que quede en la bandeja en lugar
        // de mandar un correo vacio.
        _ => throw new InvalidOperationException(
                 $"No se como redactar un aviso del tipo '{mensaje.TipoEvento}'.")
    };

    private Armado ArmarCita(string payload)
    {
        var aviso = Leer<AvisoCitaDto>(payload);

        string cuando = aviso.BloqueHorario.ToString(
            "dddd d 'de' MMMM 'de' yyyy, h:mm tt", Cultura);

        string asunto = $"Consulta programada para {aviso.Residente}";

        string filas =
            Fila("Residente", aviso.Residente) +
            Fila("Especialista", aviso.Especialista) +
            Fila("Especialidad", aviso.Especialidad) +
            Fila("Fecha y hora", cuando);

        string intro =
            $"Le informamos que se programo una consulta para <strong>{Escapar(aviso.Residente)}</strong>. " +
            "A continuacion el detalle de la cita.";

        string pie =
            "Si necesita reprogramar o tiene alguna consulta, comuniquese con la "
          + "administracion del asilo.";

        string texto =
            $"Se programo una consulta para {aviso.Residente}.\n\n"
          + $"Especialista: {aviso.Especialista}\n"
          + $"Especialidad: {aviso.Especialidad}\n"
          + $"Fecha y hora: {cuando}\n";

        return new Armado(asunto, Plantilla("Consulta programada", intro, filas, null, pie),
                          texto, aviso.Destinatarios);
    }

    private Armado ArmarFactura(string payload)
    {
        var aviso = Leer<AvisoFacturaDto>(payload);

        string periodo = $"{NombreMes(aviso.Mes)} {aviso.Anio}";
        string asunto = $"Estado de cuenta de {aviso.Residente} - {periodo}";

        // Muestro el desglose completo y no solo el total porque el
        // descuento del convenio es justamente lo que la familia querria
        // ver: si solo llegara el neto, no habria forma de notar que se
        // aplico.
        string filas =
            Fila("Residente", aviso.Residente) +
            Fila("Periodo", periodo) +
            Fila("Servicios de la fundacion", Moneda(aviso.TotalServicios));

        if (aviso.DescuentoAplicado > 0)
            filas += Fila("Descuento del convenio", "- " + Moneda(aviso.DescuentoAplicado), true);

        filas += Fila("Cuota mensual de estancia", Moneda(aviso.CuotaMensual));

        string intro =
            $"Le compartimos el estado de cuenta de <strong>{Escapar(aviso.Residente)}</strong> "
          + $"correspondiente a {Escapar(periodo)}.";

        string pie =
            "El descuento del convenio aplica unicamente sobre los servicios de la fundacion; "
          + "la cuota de estancia es un cobro del asilo y no se descuenta. "
          + "Para realizar su pago, comuniquese con la administracion.";

        string texto =
            $"Estado de cuenta de {aviso.Residente} - {periodo}\n\n"
          + $"Servicios de la fundacion: {Moneda(aviso.TotalServicios)}\n"
          + (aviso.DescuentoAplicado > 0
                ? $"Descuento del convenio: -{Moneda(aviso.DescuentoAplicado)}\n" : "")
          + $"Cuota mensual de estancia: {Moneda(aviso.CuotaMensual)}\n"
          + $"TOTAL A PAGAR: {Moneda(aviso.TotalNeto)}\n";

        return new Armado(
            asunto,
            Plantilla("Estado de cuenta", intro, filas,
                      new Total("Total a pagar", Moneda(aviso.TotalNeto)), pie),
            texto, aviso.Destinatarios);
    }

    private Armado ArmarPago(string payload)
    {
        var aviso = Leer<AvisoPagoDto>(payload);

        bool saldada = aviso.SaldoRestante <= 0;

        string asunto = saldada
            ? $"Pago recibido - cuenta de {aviso.Residente} al dia"
            : $"Pago recibido de {aviso.Residente}";

        string filas =
            Fila("Residente", aviso.Residente) +
            Fila("Monto recibido", Moneda(aviso.MontoRecibido)) +
            Fila("Metodo de pago", aviso.MetodoPago) +
            Fila("Fecha", DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", Cultura));

        string intro =
            $"Confirmamos la recepcion de su pago para la cuenta de "
          + $"<strong>{Escapar(aviso.Residente)}</strong>. Gracias.";

        // El saldo es lo que de verdad le importa a quien paga, asi que
        // va destacado y no como una fila mas. Cuando queda en cero lo
        // digo con todas sus letras en lugar de mostrar "Q 0.00", que
        // obliga a interpretar.
        var total = saldada
            ? new Total("Saldo pendiente", "Ninguno. Cuenta al dia.")
            : new Total("Saldo pendiente", Moneda(aviso.SaldoRestante));

        string pie = saldada
            ? "No queda saldo pendiente en esta cuenta. Conserve este correo como constancia."
            : "Puede realizar abonos parciales. Para cualquier aclaracion, comuniquese con la "
            + "administracion del asilo.";

        string texto =
            $"Pago recibido para la cuenta de {aviso.Residente}.\n\n"
          + $"Monto recibido: {Moneda(aviso.MontoRecibido)}\n"
          + $"Metodo: {aviso.MetodoPago}\n"
          + (saldada
                ? "Saldo pendiente: ninguno. Cuenta al dia.\n"
                : $"Saldo pendiente: {Moneda(aviso.SaldoRestante)}\n");

        return new Armado(asunto, Plantilla("Confirmacion de pago", intro, filas, total, pie),
                          texto, aviso.Destinatarios);
    }

    // ---------------- Plantilla HTML ----------------

    private record Total(string Rotulo, string Valor);

    /// <summary>
    /// El correo se arma con tablas y estilos escritos en cada etiqueta.
    /// No es descuido: los clientes de correo no cargan hojas externas y
    /// varios ignoran el bloque style del encabezado, asi que lo unico
    /// que se respeta en todos es el atributo style de cada elemento.
    /// </summary>
    private static string Plantilla(string titulo, string intro, string filas,
                                    Total? total, string pie)
    {
        string bloqueTotal = total is null ? "" : $@"
          <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:18px"">
            <tr>
              <td style=""background:#FDEDE6;border-left:4px solid #F2622D;padding:14px 16px"">
                <div style=""font-size:12px;letter-spacing:.06em;text-transform:uppercase;
                            color:#5C564C"">{Escapar(total.Rotulo)}</div>
                <div style=""font-size:24px;font-weight:600;color:#C94A1B;margin-top:4px"">
                  {Escapar(total.Valor)}</div>
              </td>
            </tr>
          </table>";

        return $@"<!doctype html>
<html lang=""es"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#F2EDE4;
             font-family:'Segoe UI',Helvetica,Arial,sans-serif;color:#1A1A1A"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#F2EDE4;padding:24px 12px"">
    <tr><td align=""center"">
      <table width=""100%"" cellpadding=""0"" cellspacing=""0""
             style=""max-width:560px;background:#FFFFFF;border:1px solid #DDD5C7;border-radius:6px"">

        <tr><td style=""background:#1A1A1A;padding:18px 24px;border-radius:6px 6px 0 0"">
          <div style=""font-size:11px;letter-spacing:.14em;text-transform:uppercase;
                      color:#D9CDB8"">Asilo</div>
          <div style=""font-size:19px;font-weight:600;color:#FFFFFF;margin-top:2px"">
            Cabeza de Algodon</div>
        </td></tr>

        <tr><td style=""height:3px;background:#F2622D;font-size:0;line-height:0"">&nbsp;</td></tr>

        <tr><td style=""padding:24px"">
          <h1 style=""margin:0 0 12px;font-size:18px;font-weight:600;color:#1A1A1A"">
            {Escapar(titulo)}</h1>

          <p style=""margin:0 0 18px;font-size:14px;line-height:1.6;color:#5C564C"">{intro}</p>

          <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-size:14px"">{filas}</table>
          {bloqueTotal}

          <p style=""margin:20px 0 0;font-size:13px;line-height:1.6;color:#8C8578"">
            {Escapar(pie)}</p>
        </td></tr>

        <tr><td style=""padding:14px 24px;background:#F7F3EC;border-top:1px solid #DDD5C7;
                       border-radius:0 0 6px 6px"">
          <p style=""margin:0;font-size:11px;line-height:1.5;color:#8C8578"">
            Este es un mensaje automatico del sistema de administracion del asilo.
            Por favor no responda a esta direccion.</p>
        </td></tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";
    }

    private static string Fila(string rotulo, string valor, bool resaltado = false)
    {
        string color = resaltado ? "#3D6B2E" : "#1A1A1A";

        return $@"
          <tr>
            <td style=""padding:9px 0;border-bottom:1px solid #DDD5C7;color:#8C8578;
                       width:45%;vertical-align:top"">{Escapar(rotulo)}</td>
            <td style=""padding:9px 0;border-bottom:1px solid #DDD5C7;color:{color};
                       font-weight:500;text-align:right"">{Escapar(valor)}</td>
          </tr>";
    }

    // ---------------- Utilidades ----------------

    private static readonly CultureInfo Cultura = new("es-GT");

    private static string Moneda(decimal monto) =>
        "Q " + monto.ToString("N2", CultureInfo.InvariantCulture);

    private static string NombreMes(int mes) =>
        mes >= 1 && mes <= 12
            ? Cultura.DateTimeFormat.GetMonthName(mes)
            : mes.ToString();

    /// <summary>
    /// Escapo todo lo que viene de la base antes de meterlo en el HTML.
    /// Un nombre con un ampersand o un signo de menor que romperia el
    /// correo, y el contenido lo escriben usuarios.
    /// </summary>
    private static string Escapar(string? texto) =>
        System.Net.WebUtility.HtmlEncode(texto ?? string.Empty);

    private static T Leer<T>(string payload) where T : new() =>
        JsonSerializer.Deserialize<T>(payload, Formato)
        ?? throw new InvalidOperationException(
               $"El contenido del aviso no se pudo leer como {typeof(T).Name}.");
}
