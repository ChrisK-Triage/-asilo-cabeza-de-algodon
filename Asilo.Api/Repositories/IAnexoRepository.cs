using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IAnexoRepository
{
    // Laboratorio
    Task<IEnumerable<OrdenDto>> ObtenerOrdenesAsync(EstadoOrden? estado, int? citaId);
    Task<OrdenDto?> ObtenerOrdenPorIdAsync(int id);
    Task<int> CrearOrdenAsync(OrdenLaboratorio orden);
    Task<bool> CargarResultadoAsync(int id, string resultado, decimal costo);

    // Farmacia
    Task<IEnumerable<DespachoDto>> ObtenerDespachosAsync(int? citaId);
    Task<DespachoDto?> ObtenerDespachoPorIdAsync(int id);
    Task<int> CrearDespachoAsync(DespachoFarmacia despacho);

    // Comun
    Task<ResumenAnexosDto?> ObtenerResumenAsync(int citaId);
    Task<bool> CitaExisteAsync(int citaId);

    // Estado de la cita frente a los anexos: si existe, si tiene
    // diagnostico, que se le indico y si ya se cobro.
    Task<EstadoAnexosDto?> ObtenerEstadoAnexosAsync(int citaId);
    Task<bool> CitaTieneDiagnosticoAsync(int citaId);
    Task<bool> CitaLiquidadaAsync(int citaId);

    // Evalua si la cita ya recibio todo lo que el especialista le pidio
    // y, en ese caso, la pasa a BitacoraCompletada. Devuelve true solo
    // cuando efectivamente la cerro.
    Task<bool> CerrarBitacoraSiCompletaAsync(int citaId);
}
