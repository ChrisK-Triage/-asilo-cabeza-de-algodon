using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface ICitaRepository
{
    Task<IEnumerable<CitaDto>> ObtenerTodasAsync(EstadoCita? estado, int? especialistaId);
    Task<CitaDto?> ObtenerPorIdAsync(int id);
    Task<int> AsignarAsync(CitaFundacion cita);
    Task<bool> RegistrarDiagnosticoAsync(int id, string diagnostico, bool requiereLaboratorio, bool requiereFarmacia);
    Task<IEnumerable<EspecialistaDto>> ObtenerEspecialistasAsync();
    Task<bool> HorarioOcupadoAsync(int especialistaId, DateTime bloque, int? excluirCitaId = null);
    Task<bool> RemisionYaTieneCitaAsync(int remisionId);
}
