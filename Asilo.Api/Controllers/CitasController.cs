using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/citas")]
[Produces("application/json")]
public class CitasController : ControllerBase
{
    private readonly ICitaRepository _repo;
    private readonly IRemisionRepository _repoRemision;

    public CitasController(ICitaRepository repo, IRemisionRepository repoRemision)
    {
        _repo = repo;
        _repoRemision = repoRemision;
    }

    /// <summary>Lista las citas. Filtros opcionales por estado y especialista.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CitaDto>>> Get(
        [FromQuery] EstadoCita? estado,
        [FromQuery] int? especialistaId)
        => Ok(await _repo.ObtenerTodasAsync(estado, especialistaId));

    /// <summary>Obtiene una cita por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CitaDto>> GetById(int id)
    {
        var cita = await _repo.ObtenerPorIdAsync(id);

        return cita is null
            ? NotFound(new { mensaje = $"No existe una cita con Id {id}." })
            : Ok(cita);
    }

    /// <summary>Catalogo de especialistas para alimentar el select de la consola.</summary>
    [HttpGet("especialistas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EspecialistaDto>>> GetEspecialistas()
        => Ok(await _repo.ObtenerEspecialistasAsync());

    /// <summary>
    /// La fundacion asigna especialista y horario a una remision pendiente (RF-03).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CitaDto>> Post(AsignarCitaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // 1. La remision debe existir
        var remision = await _repoRemision.ObtenerPorIdAsync(dto.RemisionId);
        if (remision is null)
            return BadRequest(new { mensaje = $"La remision con Id {dto.RemisionId} no existe." });

        // 2. Solo se asignan remisiones pendientes. Una cancelada o ya
        //    programada no deberia volver a entrar al flujo.
        if (remision.Estado != "Pendiente")
            return Conflict(new { mensaje = $"La remision ya esta en estado {remision.Estado}. Solo se asignan remisiones pendientes." });

        // 3. Relacion 1 a 0..1: una remision origina como maximo una cita
        if (await _repo.RemisionYaTieneCitaAsync(dto.RemisionId))
            return Conflict(new { mensaje = "Esta remision ya tiene una cita asignada." });

        // 4. El especialista debe existir y tener ese rol
        var especialistas = await _repo.ObtenerEspecialistasAsync();
        var especialista = especialistas.FirstOrDefault(e => e.Id == dto.EspecialistaId);
        if (especialista is null)
            return BadRequest(new { mensaje = $"El especialista con Id {dto.EspecialistaId} no existe o no esta activo." });

        // 5. Choque de horario. Es la excepcion A1 del caso de uso CU-02.
        if (await _repo.HorarioOcupadoAsync(dto.EspecialistaId, dto.BloqueHorario))
            return Conflict(new { mensaje = $"{especialista.NombreCompleto} ya tiene una cita asignada en ese horario." });

        // 6. La cita no puede agendarse en el pasado
        if (dto.BloqueHorario < DateTime.Now)
            return BadRequest(new { mensaje = "El bloque horario no puede ser anterior a la fecha actual." });

        var cita = new CitaFundacion
        {
            RemisionId = dto.RemisionId,
            EspecialistaId = dto.EspecialistaId,
            BloqueHorario = dto.BloqueHorario,
            CostoConsulta = dto.CostoConsulta,
            Estado = EstadoCita.Programada,
            Liquidada = false
        };

        int id = await _repo.AsignarAsync(cita);
        var creada = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creada);
    }

    /// <summary>
    /// El especialista registra el diagnostico de la consulta (RF-04).
    /// </summary>
    [HttpPut("{id:int}/diagnostico")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutDiagnostico(int id, DiagnosticoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cita = await _repo.ObtenerPorIdAsync(id);
        if (cita is null)
            return NotFound(new { mensaje = $"No existe una cita con Id {id}." });

        // Un diagnostico ya asentado no se sobrescribe: el expediente
        // clinico es un registro historico, no un borrador.
        if (!string.IsNullOrWhiteSpace(cita.Diagnostico))
            return Conflict(new { mensaje = "Esta cita ya tiene un diagnostico registrado. Una correccion debe asentarse como nota nueva." });

        await _repo.RegistrarDiagnosticoAsync(id, dto.Diagnostico.Trim(), dto.RequiereAnexos);
        return NoContent();
    }
}
