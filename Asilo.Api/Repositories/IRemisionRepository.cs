using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IRemisionRepository
{
    Task<IEnumerable<RemisionDto>> ObtenerTodasAsync(EstadoRemision? estado, int? residenteId);
    Task<RemisionDto?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(Remision remision);
    Task<bool> CambiarEstadoAsync(int id, EstadoRemision estado);
    Task<bool> ExisteAsync(int id);
}
