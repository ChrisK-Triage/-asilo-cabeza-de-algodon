using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/laboratorio")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.Anexos)]
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
    [Authorize(Roles = RolesUsuario.Permiso.Consulta)]
    [HttpPost("ordenes")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdenDto>> PostOrden(OrdenCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Una sola consulta en lugar de tres: necesito el estado completo
        // de la cita para decidir si todavia admite un examen.
        var cita = await _repo.ObtenerEstadoAnexosAsync(dto.CitaId);
        if (cita is null)
            return BadRequest(new { mensaje = $"La cita con Id {dto.CitaId} no existe." });

        // El examen lo indica el especialista durante la consulta. Sin
        // diagnostico registrado no hubo valoracion que lo justifique.
        if (!cita.TieneDiagnostico)
            return Conflict(new { mensaje = "La cita aun no tiene consulta registrada. El especialista debe valorar al residente antes de solicitar examenes." });

        // Si la cita ya se cobro, agregarle cargos la dejaria con un total
        // distinto al que se le facturo al familiar.
        if (cita.Liquidada)
            return Conflict(new { mensaje = "Esta cita ya fue liquidada. No se pueden agregar cargos nuevos." });

        // El especialista no pidio laboratorio en esta cita. Un examen que
        // nadie indico no tiene quien lo respalde, y el costo igual se le
        // facturaria al familiar.
        if (!cita.RequiereLaboratorio)
            return Conflict(new { mensaje = "El especialista no indico examenes de laboratorio para esta cita." });

        // 2 = EsperandoAnexos. Con la bitacora ya cerrada, la consulta
        // quedo asentada como completa: agregarle un examen despues
        // modificaria un expediente terminado y un total ya calculado.
        if (cita.Estado != 2)
            return Conflict(new { mensaje = "La consulta de esta cita ya esta cerrada. Un examen posterior debe solicitarse desde una remision nueva." });

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
    [Authorize(Roles = RolesUsuario.Permiso.Procesamiento)]
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
