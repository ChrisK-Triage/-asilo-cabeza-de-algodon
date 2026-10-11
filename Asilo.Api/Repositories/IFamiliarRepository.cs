using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IFamiliarRepository
{
    Task<IEnumerable<FamiliarDto>> ObtenerPorResidenteAsync(int residenteId);
    Task<FamiliarDto?> ObtenerPorIdAsync(int id);
    Task<int> CrearAsync(Familiar familiar);
    Task<bool> ActualizarAsync(int id, FamiliarUpdateDto dto);
    Task<bool> EliminarAsync(int id);
    Task<bool> EmailRepetidoAsync(int residenteId, string email, int? excluirId = null);
}
