namespace Asilo.Api.Services;

/// <summary>
/// Los datos del servidor de correo, leidos de configuracion.
///
/// Van aparte de OpcionesNotificaciones porque son cosas distintas: alla
/// esta el ritmo del despachador —cada cuanto revisa, cuantos toma,
/// cuantas veces reintenta— y aca por donde sale el correo. Si manana se
/// cambia de Gmail a otro proveedor, se toca este archivo y nada mas.
/// </summary>
public class OpcionesCorreo
{
    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;

    /// <summary>
    /// La cuenta con la que me autentico. En Gmail es el correo
    /// completo.
    /// </summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>
    /// La contrasena de aplicacion, no la de la cuenta. Nunca va en
    /// appsettings.json: en mi maquina esta en appsettings.Development.json,
    /// que esta en el .gitignore, y en Azure es una variable de entorno.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Lo que ve el familiar en el remitente.</summary>
    public string NombreRemitente { get; set; } = "Asilo Cabeza de Algodon";
    public string CorreoRemitente { get; set; } = string.Empty;

    /// <summary>
    /// Cuando esta lleno, todos los avisos se mandan aqui en lugar de a
    /// los familiares. Es para probar sin escribirle a nadie real: el
    /// asunto lleva delante a quien le habria llegado.
    /// </summary>
    public string? RedirigirTodoA { get; set; }

    /// <summary>
    /// Segundos antes de darme por vencido con el servidor. El
    /// despachador reintenta, asi que prefiero cortar pronto y no dejar
    /// la vuelta colgada esperando a un servidor que no responde.
    /// </summary>
    public int TiempoLimiteSegundos { get; set; } = 20;

    public const string Seccion = "Correo";

    /// <summary>
    /// Sin usuario, password o remitente no hay forma de mandar nada.
    /// Lo compruebo al arrancar para no descubrirlo cuando ya hay avisos
    /// encolados fallando uno por uno.
    /// </summary>
    public bool EstaCompleta() =>
        !string.IsNullOrWhiteSpace(Servidor)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(CorreoRemitente);
}
