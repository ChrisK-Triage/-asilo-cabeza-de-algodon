using Asilo.Api.DTOs;

namespace Asilo.Api.Repositories;

public interface IFacturaRepository
{
    /// <summary>
    /// Calcula lo que le corresponde pagar a un residente en un periodo,
    /// sin emitir nada. Es la vista previa que ve el administrador antes
    /// de confirmar la liquidacion.
    /// </summary>
    Task<CalculoPeriodoDto?> CalcularPeriodoAsync(int residenteId, int anio, int mes);

    /// <summary>
    /// Emite la factura del periodo y marca como liquidadas las citas que
    /// la componen. Devuelve el Id de la factura creada.
    ///
    /// Lanza <see cref="InvalidOperationException"/> cuando otra emision
    /// cobro la estancia del mismo periodo entre el calculo y la
    /// insercion.
    /// </summary>
    Task<int> EmitirAsync(CalculoPeriodoDto calculo);

    Task<FacturaDto?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<FacturaDto>> ObtenerPorResidenteAsync(int residenteId);
    Task<EstadoCuentaDto?> ObtenerEstadoCuentaAsync(int residenteId);

    Task<IEnumerable<PagoDto>> ObtenerPagosAsync(int facturaId);
    Task<int> RegistrarPagoAsync(int facturaId, decimal monto, string metodo, int? registradoPor);
}
