using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/laboratorio")]
[Produces("application/json")]
public class LaboratorioController : ControllerBase
{
    private readonly IAnexoRepository _repo;

    public LaboratorioController(IAnexoRepository repo) => _repo = repo;

    /// <summary>Lista las ordenes de laboratorio. Filtros por estado y cita.</summary>
    [HttpGet("ordenes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrdenDto>>> GetOrdenes(
        [FromQuery] EstadoOrden? estado,
        [FromQuery] int? citaId)
        => Ok(await _repo.ObtenerOrdenesAsync(estado, citaId));

    /// <summary>Obtiene una orden por su Id.</summary>
    [HttpGet("ordenes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> GetOrden(int id)
    {
        var orden = await _repo.ObtenerOrdenPorIdAsync(id);

        return orden is null
            ? NotFound(new { mensaje = $"No existe una orden con Id {id}." })
            : Ok(orden);
    }

    /// <summary>
    /// El especialista solicita un examen durante la consulta (RF-05a).
    /// El costo no entra aqui: lo registra el tecnico al cargar el resultado.
    /// </summary>
    [HttpPost("ordenes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdenDto>> PostOrden(OrdenCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _repo.CitaExisteAsync(dto.CitaId))
            return BadRequest(new { mensaje = $"La cita con Id {dto.CitaId} no existe." });

        // El examen lo indica el especialista durante la consulta. Sin
        // diagnostico registrado no hubo valoracion que lo justifique.
        if (!await _repo.CitaTieneDiagnosticoAsync(dto.CitaId))
            return Conflict(new { mensaje = "La cita aun no tiene consulta registrada. El especialista debe valorar al residente antes de solicitar examenes." });

        // Si la cita ya se cobro, agregarle cargos la dejaria con un total
        // distinto al que se le facturo al familiar.
        if (await _repo.CitaLiquidadaAsync(dto.CitaId))
            return Conflict(new { mensaje = "Esta cita ya fue liquidada. No se pueden agregar cargos nuevos." });

        var orden = new OrdenLaboratorio
        {
            CitaId = dto.CitaId,
            TipoExamen = dto.TipoExamen.Trim(),
            Estado = EstadoOrden.Pendiente
        };

        int id = await _repo.CrearOrdenAsync(orden);
        var creada = await _repo.ObtenerOrdenPorIdAsync(id);

        return CreatedAtAction(nameof(GetOrden), new { id }, creada);
    }

    /// <summary>
    /// El tecnico de laboratorio carga el resultado y el costo del examen.
    /// </summary>
    [HttpPut("ordenes/{id:int}/resultado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutResultado(int id, ResultadoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var orden = await _repo.ObtenerOrdenPorIdAsync(id);
        if (orden is null)
            return NotFound(new { mensaje = $"No existe una orden con Id {id}." });

        // Un resultado clinico ya cargado no se sobrescribe. Si el examen
        // se repite, se solicita una orden nueva.
        if (orden.Estado == "Completada")
            return Conflict(new { mensaje = "Esta orden ya tiene resultado cargado. Para repetir el examen debe solicitarse una orden nueva." });

        if (await _repo.CitaLiquidadaAsync(orden.CitaId))
            return Conflict(new { mensaje = "La cita asociada ya fue liquidada. No se pueden modificar sus costos." });

        await _repo.CargarResultadoAsync(id, dto.Resultado.Trim(), dto.CostoBruto);
        return NoContent();
    }
}
