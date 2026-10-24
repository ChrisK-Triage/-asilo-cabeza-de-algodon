namespace Asilo.Api.Services;

/// <summary>
/// Los parametros del despachador, en un solo lugar.
///
/// El tope de intentos lo usan tres partes —el despachador para decidir
/// a quien vuelve a tomar, el repositorio para derivar la situacion
/// "Fallido" y el controlador para el resumen—, y si cada una tuviera su
/// propio numero, un mensaje podria verse como pendiente en la pantalla
/// y estar abandonado en realidad.
/// </summary>
public static class OpcionesNotificaciones
{
    public const int TopeIntentosPorOmision = 5;
    public const int MaximoPorVueltaPorOmision = 20;

    /// <summary>
    /// Cada cuanto barre la bandeja aunque nadie toque el timbre.
    ///
    /// Es una red de seguridad, no el modo normal de trabajar: cubre los
    /// avisos que quedaron encolados si el proceso se reinicio entre el
    /// commit y el timbre. Doce horas porque la base serverless de Azure
    /// se pausa tras una hora sin consultas, y un barrido mas frecuente
    /// la despertaria lo suficiente como para que no se duerma nunca,
    /// que es justo lo que agoto la cuota del mes pasado.
    /// </summary>
    public const int MinutosDeRespaldoPorOmision = 720;

    /// <summary>
    /// Cuanto espera antes de reintentar cuando quedaron avisos que
    /// fallaron. A estos no los anuncia nadie —el timbre suena cuando se
    /// encola algo nuevo, no cuando algo viejo sigue fallando—, asi que
    /// aqui si hace falta el reloj.
    /// </summary>
    public const int SegundosEntreReintentosPorOmision = 300;

    public static int TopeIntentos(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:TopeIntentos") ?? TopeIntentosPorOmision;

    public static int MaximoPorVuelta(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:MaximoPorVuelta") ?? MaximoPorVueltaPorOmision;

    public static bool Habilitado(IConfiguration config)
        => config.GetValue<bool?>("Notificaciones:Habilitado") ?? true;

    public static int MinutosDeRespaldo(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:MinutosDeRespaldo")
           ?? MinutosDeRespaldoPorOmision;

    public static int SegundosEntreReintentos(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:SegundosEntreReintentos")
           ?? SegundosEntreReintentosPorOmision;
}
