using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface ICatalogoRepository
{
    // Especialidades
    Task<IEnumerable<EspecialidadDto>> ObtenerEspecialidadesAsync(bool soloActivas = true);
    Task<EspecialidadDto?> ObtenerEspecialidadPorIdAsync(int id);
    Task<int> CrearEspecialidadAsync(Especialidad especialidad);
    Task<bool> DesactivarEspecialidadAsync(int id);
    Task<bool> EspecialidadRepetidaAsync(string nombre, int? excluirId = null);

    // Enfermeros
    Task<IEnumerable<EnfermeroDto>> ObtenerEnfermerosAsync(bool soloActivos = true);
    Task<EnfermeroDto?> ObtenerEnfermeroPorIdAsync(int id);
    Task<int> CrearEnfermeroAsync(Enfermero enfermero);
    Task<bool> DesactivarEnfermeroAsync(int id);
    Task<bool> EnfermeroRepetidoAsync(string nombre, int? excluirId = null);
}
