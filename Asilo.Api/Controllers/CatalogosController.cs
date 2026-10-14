using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Catalogos de especialidades y enfermeros. Los junto en un solo
/// controlador porque son tablas de apoyo con el mismo comportamiento:
/// se listan, se agregan y se dan de baja logicamente. Ninguna merece
/// un controlador propio.
/// </summary>
[ApiController]
[Route("api/catalogos")]
[Produces("application/json")]
[Authorize]
public class CatalogosController : ControllerBase
{
    private readonly ICatalogoRepository _repo;

    public CatalogosController(ICatalogoRepository repo) => _repo = repo;

    // ==================== ESPECIALIDADES ====================

    /// <summary>
    /// Lista las especialidades con el numero de especialistas activos
    /// que las atienden. El formulario de remision usa ese conteo para
    /// advertir cuales no tienen cobertura.
    /// </summary>
    [HttpGet("especialidades")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EspecialidadDto>>> GetEspecialidades(
        [FromQuery] bool incluirInactivas = false)
        => Ok(await _repo.ObtenerEspecialidadesAsync(!incluirInactivas));

    /// <summary>Obtiene una especialidad por su Id.</summary>
    [HttpGet("especialidades/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EspecialidadDto>> GetEspecialidad(int id)
    {
        var especialidad = await _repo.ObtenerEspecialidadPorIdAsync(id);

        return especialidad is null
            ? NotFound(new { mensaje = $"No existe una especialidad con Id {id}." })
            : Ok(especialidad);
    }

    /// <summary>Agrega una especialidad al catalogo.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
    [HttpPost("especialidades")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EspecialidadDto>> PostEspecialidad(EspecialidadCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // El nombre esta bien escrito; lo que no permito es duplicarlo.
        if (await _repo.EspecialidadRepetidaAsync(dto.Nombre.Trim()))
            return Conflict(new { mensaje = $"La especialidad \"{dto.Nombre.Trim()}\" ya esta en el catalogo." });

        int id = await _repo.CrearEspecialidadAsync(new Especialidad { Nombre = dto.Nombre.Trim() });
        var creada = await _repo.ObtenerEspecialidadPorIdAsync(id);

        return CreatedAtAction(nameof(GetEspecialidad), new { id }, creada);
    }

    /// <summary>
    /// Da de baja una especialidad. Baja logica: las remisiones
    /// historicas siguen apuntando a ella y tienen que poder leerse.
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
    [HttpDelete("especialidades/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEspecialidad(int id)
        => await _repo.DesactivarEspecialidadAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe una especialidad activa con Id {id}." });

    // ==================== ENFERMEROS ====================

    /// <summary>Lista los enfermeros del asilo.</summary>
    [HttpGet("enfermeros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EnfermeroDto>>> GetEnfermeros(
        [FromQuery] bool incluirInactivos = false)
        => Ok(await _repo.ObtenerEnfermerosAsync(!incluirInactivos));

    /// <summary>Obtiene un enfermero por su Id.</summary>
    [HttpGet("enfermeros/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnfermeroDto>> GetEnfermero(int id)
    {
        var enfermero = await _repo.ObtenerEnfermeroPorIdAsync(id);

        return enfermero is null
            ? NotFound(new { mensaje = $"No existe un enfermero con Id {id}." })
            : Ok(enfermero);
    }

    /// <summary>Registra un enfermero.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
    [HttpPost("enfermeros")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnfermeroDto>> PostEnfermero(EnfermeroCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _repo.EnfermeroRepetidoAsync(dto.Nombre.Trim()))
            return Conflict(new { mensaje = $"Ya hay un enfermero registrado como \"{dto.Nombre.Trim()}\"." });

        var enfermero = new Enfermero
        {
            Nombre = dto.Nombre.Trim(),
            Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim()
        };

        int id = await _repo.CrearEnfermeroAsync(enfermero);
        var creado = await _repo.ObtenerEnfermeroPorIdAsync(id);

        return CreatedAtAction(nameof(GetEnfermero), new { id }, creado);
    }

    /// <summary>Da de baja a un enfermero sin borrar su historial.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
    [HttpDelete("enfermeros/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEnfermero(int id)
        => await _repo.DesactivarEnfermeroAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un enfermero activo con Id {id}." });
}
