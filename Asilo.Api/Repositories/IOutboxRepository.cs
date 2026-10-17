using System.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IOutboxRepository
{
    /// <summary>
    /// Escribe el aviso usando la conexion y la transaccion que ya abrio
    /// el llamador.
    ///
    /// Recibirlas en lugar de abrir las suyas es lo que hace que esto sea
    /// un Outbox y no una tabla de avisos cualquiera: el INSERT entra en
    /// la misma transaccion que la cita o la factura, asi que o se
    /// guardan los dos o ninguno. Si abriera su propia conexion, un
    /// rollback de la operacion dejaria encolado un aviso de algo que
    /// nunca ocurrio.
    /// </summary>
    Task EncolarAsync(IDbConnection conexion, IDbTransaction transaccion,
                      string tipoEvento, object contenido);

    /// <summary>Destinatarios registrados para un residente.</summary>
    Task<List<DestinatarioAviso>> ObtenerDestinatariosAsync(
        IDbConnection conexion, IDbTransaction transaccion, int residenteId);

    // ---- Lo que usa el despachador ----

    Task<IEnumerable<OutboxMensaje>> TomarPendientesAsync(int maximo, int topeIntentos);
    Task MarcarEntregadoAsync(long id);
    Task RegistrarFalloAsync(long id, string error);

    // ---- Lo que usa la pantalla ----

    Task<IEnumerable<OutboxMensajeDto>> ObtenerAsync(string? situacion, int topeIntentos);
    Task<ResumenOutboxDto> ObtenerResumenAsync(int topeIntentos);
}
