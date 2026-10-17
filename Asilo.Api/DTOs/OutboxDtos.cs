namespace Asilo.Api.DTOs;

/// <summary>
/// Un mensaje de la bandeja, como se ve en la pantalla de administracion.
/// </summary>
public class OutboxMensajeDto
{
    public long Id { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public string TipoEtiqueta { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;

    /// <summary>Pendiente, Entregado o Fallido.</summary>
    public string Situacion { get; set; } = string.Empty;

    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaProcesado { get; set; }

    /// <summary>
    /// A quien iba dirigido, sacado del propio payload para que la
    /// pantalla no tenga que interpretarlo. Si el aviso se encolo sin
    /// destinatario, aqui se ve.
    /// </summary>
    public string Destinatarios { get; set; } = string.Empty;
}

/// <summary>Conteos de la bandeja, para el encabezado de la pantalla.</summary>
public class ResumenOutboxDto
{
    public int Pendientes { get; set; }
    public int Entregados { get; set; }
    public int Fallidos { get; set; }
}

// ---------------- Contenido de los avisos ----------------

/// <summary>
/// Un familiar al que hay que avisarle. Viaja dentro del payload y no
/// como referencia a la tabla: si manana se le cambia el correo, el
/// aviso ya encolado debe salir al que estaba registrado cuando ocurrio
/// el hecho.
/// </summary>
public class DestinatarioAviso
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public class AvisoCitaDto
{
    public int CitaId { get; set; }
    public string Residente { get; set; } = string.Empty;
    public string Especialista { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public DateTime BloqueHorario { get; set; }
    public List<DestinatarioAviso> Destinatarios { get; set; } = new();
}

public class AvisoFacturaDto
{
    public int FacturaId { get; set; }
    public string Residente { get; set; } = string.Empty;
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal TotalServicios { get; set; }
    public decimal DescuentoAplicado { get; set; }
    public decimal CuotaMensual { get; set; }
    public decimal TotalNeto { get; set; }
    public List<DestinatarioAviso> Destinatarios { get; set; } = new();
}

public class AvisoPagoDto
{
    public int FacturaId { get; set; }
    public string Residente { get; set; } = string.Empty;
    public decimal MontoRecibido { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public decimal SaldoRestante { get; set; }
    public List<DestinatarioAviso> Destinatarios { get; set; } = new();
}
