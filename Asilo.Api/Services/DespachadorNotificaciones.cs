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
///
/// No trabaja por reloj sino por aviso. La primera version consultaba la
/// tabla cada quince segundos todo el dia, y eso tiene un costo que no
/// se ve en desarrollo: la base serverless de Azure se pausa tras una
/// hora sin consultas y cobra por el tiempo que esta despierta, de modo
/// que preguntar seguido le impedia dormirse y agoto la cuota mensual en
/// tres dias. Ahora espera a que le toquen el timbre, y solo recurre al
/// reloj como respaldo.
/// </summary>
public class DespachadorNotificaciones : BackgroundService
{
    private readonly IServiceScopeFactory _ambitos;
    private readonly AvisadorOutbox _avisador;
    private readonly IConfiguration _config;
    private readonly ILogger<DespachadorNotificaciones> _log;

    public DespachadorNotificaciones(
        IServiceScopeFactory ambitos,
        AvisadorOutbox avisador,
        IConfiguration config,
        ILogger<DespachadorNotificaciones> log)
    {
        _ambitos = ambitos;
        _avisador = avisador;
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

        var plazoRespaldo = TimeSpan.FromMinutes(
            OpcionesNotificaciones.MinutosDeRespaldo(_config));

        var plazoReintento = TimeSpan.FromSeconds(
            OpcionesNotificaciones.SegundosEntreReintentos(_config));

        _log.LogInformation(
            "Despachador de notificaciones en marcha. Trabaja cuando se encola un aviso; "
          + "ademas barre la bandeja cada {Minutos} min por si quedo algo.",
            plazoRespaldo.TotalMinutes);

        // Un barrido al arrancar: si el proceso se reinicio con avisos
        // encolados, nadie va a tocar el timbre por ellos.
        bool hayTrabajo = true;

        while (!cancelacion.IsCancellationRequested)
        {
            if (!hayTrabajo)
            {
                // La bandeja quedo limpia. Descarto los timbrazos que se
                // acumularon mientras trabajaba —ya revise lo que
                // anunciaban— y me duermo hasta que entre algo nuevo.
                // Mientras tanto nadie consulta la base y puede pausarse.
                _avisador.Vaciar();

                try
                {
                    await _avisador.EsperarAsync(plazoRespaldo, cancelacion);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            int tomados;

            try
            {
                tomados = await DarUnaVueltaAsync(cancelacion);
            }
            catch (Exception ex)
            {
                // Un fallo de la vuelta completa —la base caida, por
                // ejemplo— no puede matar el servicio: si el bucle muere,
                // deja de haber quien entregue avisos y nadie se entera
                // hasta que un familiar reclama.
                _log.LogError(ex, "Fallo una vuelta del despachador. Reintento en la siguiente.");

                tomados = -1;
            }

            if (tomados == 0)
            {
                // Nada pendiente: a dormir hasta el proximo timbre.
                hayTrabajo = false;
                continue;
            }

            if (tomados == OpcionesNotificaciones.MaximoPorVuelta(_config))
            {
                // Tome el maximo, asi que es probable que queden mas.
                // Sigo de largo sin esperar.
                hayTrabajo = true;
                continue;
            }

            // Quedaron avisos que fallaron y hay que reintentar, o la
            // vuelta entera trono. Espero un poco antes de volver, pero
            // sin dormirme del todo: estos no los va a anunciar nadie.
            //
            // La espera es sobre el timbre y no un Task.Delay, aunque lo
            // que toca es esperar por reloj. Con Delay, el despachador
            // quedaba sordo durante esos minutos: un aviso que no se
            // puede entregar —un residente sin contacto, por ejemplo—
            // vive en la bandeja hasta agotar los intentos, asi que esta
            // rama es la normal, no la excepcional, y cualquier aviso
            // nuevo esperaba el plazo completo aunque su timbre hubiera
            // sonado al instante.
            hayTrabajo = true;

            try
            {
                await _avisador.EsperarAsync(plazoReintento, cancelacion);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log.LogInformation("Despachador de notificaciones detenido.");
    }

    /// <summary>
    /// Devuelve cuantos avisos tomo, que es como el bucle sabe si vale
    /// la pena volver enseguida o puede irse a dormir.
    /// </summary>
    private async Task<int> DarUnaVueltaAsync(CancellationToken cancelacion)
    {
        // Un ambito nuevo por vuelta: los repositorios son Scoped y este
        // servicio es Singleton, asi que no puede guardarse uno fijo.
        using var ambito = _ambitos.CreateScope();

        var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var canal = ambito.ServiceProvider.GetRequiredService<ICanalNotificacion>();

        int tope = OpcionesNotificaciones.TopeIntentos(_config);
        int maximo = OpcionesNotificaciones.MaximoPorVuelta(_config);

        var pendientes = (await outbox.TomarPendientesAsync(maximo, tope)).ToList();
        if (pendientes.Count == 0) return 0;

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

        return pendientes.Count;
    }
}
