using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IResidenteRepository
{
    Task<IEnumerable<ResidenteDto>> ObtenerTodosAsync();
    Task<ResidenteDto?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(Residente residente);
    Task<bool> ActualizarAsync(Residente residente);

    /// <summary>
    /// Actualiza solo psicopatologias y medicamentos de cajon.
    ///
    /// Es un metodo aparte y no un parametro de ActualizarAsync porque
    /// su UPDATE no nombra ninguna otra columna: aunque la peticion
    /// trajera una cuota o un DPI, no hay sentencia que los escriba.
    /// </summary>
    Task<bool> ActualizarDatosClinicosAsync(int id, string? psicopatologias, string? medicamentosCajon);

    Task<bool> DesactivarAsync(int id);
    Task<bool> ExisteAsync(int id);
}
