using Asilo.Api.Repositories;

namespace Asilo.Api.Services;

/// <summary>
/// La otra mitad del patron Outbox (RF-08): el proceso que toma los
/// avisos encolados y los entrega.
///
/// Corre aparte de las peticiones HTTP a proposito. Si el envio ocurriera
/// dentro de la peticion que asigna la cita, la fundacion tendria que
/// esperar a que el correo salga, y un servidor de correo lento volveria
/// lenta una operacion que no tiene nada que ver con el.
///
/// El enunciado habla de un microservicio de notificaciones. Aqui vive
/// dentro del mismo proceso, que es lo razonable para el tamano del
/// sistema; separarlo despues no toca nada mas que este archivo, porque
/// lo unico que comparte con el resto es la tabla.
/// </summary>
public class DespachadorNotificaciones : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly IConfiguration _config;
    private readonly ILogger<DespachadorNotificaciones> _log;

    public DespachadorNotificaciones(
        IServiceScopeFactory ambitos,
        IConfiguration config,
        ILogger<DespachadorNotificaciones> log)
    {
        _ambitos = ambitos;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        if (!OpcionesNotificaciones.Habilitado(_config))
        {
            _log.LogWarning("Despachador de notificaciones apagado por configuracion.");
            return;
        }

        var espera = TimeSpan.FromSeconds(OpcionesNotificaciones.SegundosEntreVueltas(_config));

        _log.LogInformation(
            "Despachador de notificaciones en marcha. Revisa la bandeja cada {Segundos} s.",
            espera.TotalSeconds);

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                await DarUnaVueltaAsync(cancelacion);
            }
            catch (Exception ex)
            {
                // Un fallo de la vuelta completa —la base caida, por
                // ejemplo— no puede matar el servicio: si el bucle muere,
                // deja de haber quien entregue avisos y nadie se entera
                // hasta que un familiar reclama.
                _log.LogError(ex, "Fallo una vuelta del despachador. Reintento en la siguiente.");
            }

            try
            {
                await Task.Delay(espera, cancelacion);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _log.LogInformation("Despachador de notificaciones detenido.");
    }

    private async Task DarUnaVueltaAsync(CancellationToken cancelacion)
    {
        // Un ambito nuevo por vuelta: los repositorios son Scoped y este
        // servicio es Singleton, asi que no puede guardarse uno fijo.
        using var ambito = _ambitos.CreateScope();

        var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var canal = ambito.ServiceProvider.GetRequiredService<ICanalNotificacion>();

        int tope = OpcionesNotificaciones.TopeIntentos(_config);
        int maximo = OpcionesNotificaciones.MaximoPorVuelta(_config);

        var pendientes = (await outbox.TomarPendientesAsync(maximo, tope)).ToList();
        if (pendientes.Count == 0) return;

        foreach (var mensaje in pendientes)
        {
            if (cancelacion.IsCancellationRequested) break;

            try
            {
                await canal.EnviarAsync(mensaje, cancelacion);
                await outbox.MarcarEntregadoAsync(mensaje.Id);
            }
            catch (Exception ex)
            {
                // Se marca el fallo y se sigue con los demas. Un aviso
                // que no sale no debe frenar la cola entera.
                await outbox.RegistrarFalloAsync(mensaje.Id, ex.Message);

                int intentos = mensaje.Intentos + 1;

                if (intentos >= tope)
                {
                    _log.LogError(
                        "Aviso {Id} ({Tipo}) abandonado tras {Intentos} intentos: {Motivo}",
                        mensaje.Id, mensaje.TipoEvento, intentos, ex.Message);
                }
                else
                {
                    _log.LogWarning(
                        "Aviso {Id} ({Tipo}) fallo en el intento {Intentos} de {Tope}: {Motivo}",
                        mensaje.Id, mensaje.TipoEvento, intentos, tope, ex.Message);
                }
            }
        }
    }
}
