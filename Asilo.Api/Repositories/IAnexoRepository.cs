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
    Task<bool> CitaTieneDiagnosticoAsync(int citaId);
    Task<bool> CitaLiquidadaAsync(int citaId);
}
