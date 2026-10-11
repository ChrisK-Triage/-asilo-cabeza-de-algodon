using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/farmacia")]
[Produces("application/json")]
public class FarmaciaController : ControllerBase
{
    private readonly IAnexoRepository _repo;

    public FarmaciaController(IAnexoRepository repo) => _repo = repo;

    /// <summary>Lista los despachos de medicamentos. Filtro opcional por cita.</summary>
    [HttpGet("despachos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DespachoDto>>> GetDespachos([FromQuery] int? citaId)
        => Ok(await _repo.ObtenerDespachosAsync(citaId));

    /// <summary>Obtiene un despacho por su Id.</summary>
    [HttpGet("despachos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DespachoDto>> GetDespacho(int id)
    {
        var despacho = await _repo.ObtenerDespachoPorIdAsync(id);

        return despacho is null
            ? NotFound(new { mensaje = $"No existe un despacho con Id {id}." })
            : Ok(despacho);
    }

    /// <summary>
    /// El tecnico de farmacia registra el medicamento despachado (RF-05b).
    /// El costo total lo calcula SQL Server como columna persistida.
    /// </summary>
    [HttpPost("despachos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DespachoDto>> PostDespacho(DespachoCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cita = await _repo.ObtenerEstadoAnexosAsync(dto.CitaId);
        if (cita is null)
            return BadRequest(new { mensaje = $"La cita con Id {dto.CitaId} no existe." });

        // El medicamento lo receta el especialista. Despacharlo sin consulta
        // registrada equivale a entregar un farmaco que nadie indico.
        if (!cita.TieneDiagnostico)
            return Conflict(new { mensaje = "La cita aun no tiene consulta registrada. El especialista debe valorar al residente antes de recetar medicamentos." });

        if (cita.Liquidada)
            return Conflict(new { mensaje = "Esta cita ya fue liquidada. No se pueden agregar cargos nuevos." });

        // Misma regla que en laboratorio: solo se despacha lo que el
        // especialista receto, y solo mientras la bitacora siga abierta.
        if (!cita.RequiereFarmacia)
            return Conflict(new { mensaje = "El especialista no receto medicamentos para esta cita." });

        if (cita.Estado != 2)
            return Conflict(new { mensaje = "La consulta de esta cita ya esta cerrada. Un medicamento posterior debe despacharse desde una remision nueva." });

        var despacho = new DespachoFarmacia
        {
            CitaId = dto.CitaId,
            Medicamento = dto.Medicamento.Trim(),
            Cantidad = dto.Cantidad,
            TiempoAplicacion = dto.TiempoAplicacion?.Trim(),
            CostoUnitario = dto.CostoUnitario
        };

        int id = await _repo.CrearDespachoAsync(despacho);
        var creado = await _repo.ObtenerDespachoPorIdAsync(id);

        return CreatedAtAction(nameof(GetDespacho), new { id }, creado);
    }

    /// <summary>
    /// Resumen de costos acumulados de una cita: consulta, laboratorio y
    /// farmacia. Es la base de la calculadora financiera.
    /// </summary>
    [HttpGet("resumen/{citaId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResumenAnexosDto>> GetResumen(int citaId)
    {
        var resumen = await _repo.ObtenerResumenAsync(citaId);

        return resumen is null
            ? NotFound(new { mensaje = $"No existe una cita con Id {citaId}." })
            : Ok(resumen);
    }
}
