namespace Asilo.Api.Services;

/// <summary>
/// El timbre del despachador: le avisa que acaba de entrar algo a la
/// bandeja, para que no tenga que estar preguntando.
///
/// Antes el despachador consultaba la tabla cada quince segundos todo el
/// dia. Contra mi SQL Server local eso no cuesta nada, pero la base
/// serverless de Azure cobra por segundo que esta despierta y se pausa
/// sola tras una hora sin consultas: preguntar seguido le impedia
/// dormirse y se comio la cuota del mes en tres dias.
///
/// Lo que cambia es quien toma la iniciativa. Quien encola el aviso ya
/// tiene la base abierta —la desperto la operacion que creo la cita—,
/// asi que tocar el timbre en ese momento no cuesta nada. Cuando no pasa
/// nada, nadie pregunta y la base duerme.
///
/// Se registra como Singleton: el semaforo tiene que ser el mismo para
/// todos, o cada peticion tocaria un timbre que nadie escucha.
/// </summary>
public class AvisadorOutbox
{
    /// <summary>
    /// Arranca en cero y crece con cada aviso encolado. Uso un semaforo
    /// y no una bandera porque el semaforo recuerda: si entran tres
    /// avisos mientras el despachador trabaja, no se pierde ninguno.
    /// </summary>
    private readonly SemaphoreSlim _timbre = new(0);

    /// <summary>
    /// Se llama DESPUES del commit, nunca antes.
    ///
    /// Si se tocara dentro de la transaccion, el despachador podria
    /// despertar y consultar la tabla mientras la fila todavia no esta
    /// confirmada: no veria nada, se volveria a dormir, y el aviso se
    /// quedaria esperando el barrido de respaldo.
    /// </summary>
    public void HayAlgoNuevo() => _timbre.Release();

    /// <summary>
    /// Espera a que toquen el timbre, o a que se cumpla el plazo.
    /// Devuelve true si fue el timbre.
    ///
    /// El plazo es la red de seguridad: cubre los avisos que fallaron y
    /// hay que reintentar, y los que quedaron encolados si el proceso se
    /// reinicio entre el commit y el timbre.
    /// </summary>
    public Task<bool> EsperarAsync(TimeSpan plazo, CancellationToken cancelacion)
        => _timbre.WaitAsync(plazo, cancelacion);

    /// <summary>
    /// Vacia los timbrazos acumulados. Lo llamo despues de dejar la
    /// bandeja limpia: ya revise todo lo que habia, asi que las señales
    /// pendientes corresponden a avisos que ya entregue y volverian a
    /// despertarme para nada.
    /// </summary>
    public void Vaciar()
    {
        while (_timbre.CurrentCount > 0 && _timbre.Wait(0)) { }
    }
}
