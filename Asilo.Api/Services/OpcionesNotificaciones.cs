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
    public const int SegundosPorOmision = 15;
    public const int MaximoPorVueltaPorOmision = 20;

    public static int TopeIntentos(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:TopeIntentos") ?? TopeIntentosPorOmision;

    public static int SegundosEntreVueltas(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:SegundosEntreVueltas") ?? SegundosPorOmision;

    public static int MaximoPorVuelta(IConfiguration config)
        => config.GetValue<int?>("Notificaciones:MaximoPorVuelta") ?? MaximoPorVueltaPorOmision;

    public static bool Habilitado(IConfiguration config)
        => config.GetValue<bool?>("Notificaciones:Habilitado") ?? true;
}
