using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/remisiones")]
[Produces("application/json")]
public class RemisionesController : ControllerBase
{
    private readonly IRemisionRepository _repo;
    private readonly IResidenteRepository _repoResidente;

    public RemisionesController(IRemisionRepository repo, IResidenteRepository repoResidente)
    {
        _repo = repo;
        _repoResidente = repoResidente;
    }

    /// <summary>Lista las remisiones. Filtros opcionales por estado y por residente.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RemisionDto>>> Get(
        [FromQuery] EstadoRemision? estado,
        [FromQuery] int? residenteId)
        => Ok(await _repo.ObtenerTodasAsync(estado, residenteId));

    /// <summary>Obtiene una remision por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RemisionDto>> GetById(int id)
    {
        var remision = await _repo.ObtenerPorIdAsync(id);

        return remision is null
            ? NotFound(new { mensaje = $"No existe una remision con Id {id}." })
            : Ok(remision);
    }

    /// <summary>Crea una remision a especialista (RF-02).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RemisionDto>> Post(RemisionCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Valido la llave foranea antes de insertar. Si dejo que falle la
        // restriccion de SQL Server, el usuario recibe un 500 ilegible en
        // lugar de un mensaje que le diga que corregir.
        if (!await _repoResidente.ExisteAsync(dto.ResidenteId))
            return BadRequest(new { mensaje = $"El residente con Id {dto.ResidenteId} no existe." });

        var remision = new Remision
        {
            ResidenteId = dto.ResidenteId,
            MedicoGeneralId = dto.MedicoGeneralId,
            Sintomatologia = dto.Sintomatologia.Trim(),
            EspecialidadSolicitada = dto.EspecialidadSolicitada.Trim(),
            EnfermeroAsignado = dto.EnfermeroAsignado?.Trim(),
            Estado = EstadoRemision.Pendiente
        };

        int id = await _repo.CrearAsync(remision);
        var creada = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creada);
    }

    /// <summary>Cambia el estado de una remision (la fundacion la programa o se cancela).</summary>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _repo.ExisteAsync(id))
            return NotFound(new { mensaje = $"No existe una remision con Id {id}." });

        await _repo.CambiarEstadoAsync(id, dto.Estado);
        return NoContent();
    }
}
