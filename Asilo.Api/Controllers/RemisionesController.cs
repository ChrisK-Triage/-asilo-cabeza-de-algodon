using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/remisiones")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.Remisiones)]
public class RemisionesController : ControllerBase
{
    private readonly IRemisionRepository _repo;
    private readonly IResidenteRepository _repoResidente;
    private readonly ICatalogoRepository _catalogo;

    public RemisionesController(
        IRemisionRepository repo,
        IResidenteRepository repoResidente,
        ICatalogoRepository catalogo)
    {
        _repo = repo;
        _repoResidente = repoResidente;
        _catalogo = catalogo;
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
    [Authorize(Roles = RolesUsuario.Permiso.Clinico)]
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

        // La especialidad tiene que venir del catalogo. Antes era texto
        // libre y se podia escribir cualquier cosa.
        var especialidad = await _catalogo.ObtenerEspecialidadPorIdAsync(dto.EspecialidadId);
        if (especialidad is null || !especialidad.Activa)
            return BadRequest(new { mensaje = $"La especialidad con Id {dto.EspecialidadId} no existe o esta dada de baja." });

        // El enfermero es opcional, pero si viene tiene que existir.
        if (dto.EnfermeroId.HasValue)
        {
            var enfermero = await _catalogo.ObtenerEnfermeroPorIdAsync(dto.EnfermeroId.Value);
            if (enfermero is null || !enfermero.Activo)
                return BadRequest(new { mensaje = $"El enfermero con Id {dto.EnfermeroId} no existe o esta dado de baja." });
        }

        var remision = new Remision
        {
            ResidenteId = dto.ResidenteId,
            MedicoGeneralId = dto.MedicoGeneralId,
            Sintomatologia = dto.Sintomatologia.Trim(),
            EspecialidadId = dto.EspecialidadId,
            EnfermeroId = dto.EnfermeroId,
            Estado = EstadoRemision.Pendiente
        };

        int id = await _repo.CrearAsync(remision);
        var creada = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creada);
    }

    /// <summary>
    /// Cancela una remision pendiente.
    ///
    /// Solo permite pasar a Cancelada. El paso a Programada lo hace
    /// POST /api/citas al asignar especialista y horario: una remision
    /// programada sin cita no tiene sentido, desaparece de la consola de
    /// la fundacion y no hay nada que atender.
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var remision = await _repo.ObtenerPorIdAsync(id);
        if (remision is null)
            return NotFound(new { mensaje = $"No existe una remision con Id {id}." });

        // Programar exige crear la cita. Este endpoint no puede hacerlo.
        if (dto.Estado == EstadoRemision.Programada)
            return BadRequest(new
            {
                mensaje = "Para programar una remision debe asignarse especialista y horario desde POST /api/citas. " +
                          "Este endpoint solo permite cancelar."
            });

        if (dto.Estado == EstadoRemision.Pendiente)
            return BadRequest(new { mensaje = "Una remision no puede devolverse a estado Pendiente." });

        // A partir de aqui solo queda Cancelada.
        if (remision.Estado == "Cancelada")
            return Conflict(new { mensaje = "Esta remision ya esta cancelada." });

        if (remision.Estado == "Programada")
            return Conflict(new
            {
                mensaje = "Esta remision ya tiene cita asignada. Para anularla debe cancelarse la cita, " +
                          "no la remision."
            });

        await _repo.CambiarEstadoAsync(id, EstadoRemision.Cancelada);
        return NoContent();
    }
}
