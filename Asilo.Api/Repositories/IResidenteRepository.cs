using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IResidenteRepository
{
    Task<IEnumerable<ResidenteDto>> ObtenerTodosAsync();
    Task<ResidenteDto?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(Residente residente);
    Task<bool> ActualizarAsync(Residente residente);
    Task<bool> DesactivarAsync(int id);
    Task<bool> ExisteAsync(int id);
}
