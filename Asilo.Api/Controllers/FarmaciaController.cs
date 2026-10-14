using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/farmacia")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.Anexos)]
public class FarmaciaController : ControllerBase
{
    private readonly IAnexoRepository _repo;

    public FarmaciaController(IAnexoRepository repo) => _repo = repo;

    /// <summary>Lista las recetas y los despachos. Filtro opcional por cita.</summary>
    [HttpGet("despachos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DespachoDto>>> GetDespachos([FromQuery] int? citaId)
        => Ok(await _repo.ObtenerDespachosAsync(citaId));

    /// <summary>Obtiene una receta o despacho por su Id.</summary>
    [HttpGet("despachos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DespachoDto>> GetDespacho(int id)
    {
        var despacho = await _repo.ObtenerDespachoPorIdAsync(id);

        return despacho is null
            ? NotFound(new { mensaje = $"No existe una receta con Id {id}." })
            : Ok(despacho);
    }

    /// <summary>
    /// El especialista receta el medicamento durante la consulta (RF-05b).
    ///
    /// Aqui no entra el costo: el precio lo pone farmacia al entregar.
    /// Antes esta pantalla se la pedia al tecnico junto con el nombre del
    /// medicamento y la dosis, lo que equivalia a que quien despacha
    /// inventara la indicacion medica.
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.Consulta)]
    [HttpPost("recetas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DespachoDto>> PostReceta(RecetaCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cita = await _repo.ObtenerEstadoAnexosAsync(dto.CitaId);
        if (cita is null)
            return BadRequest(new { mensaje = $"La cita con Id {dto.CitaId} no existe." });

        if (!cita.TieneDiagnostico)
            return Conflict(new { mensaje = "La cita aun no tiene consulta registrada. Hay que valorar al residente antes de recetar." });

        if (cita.Liquidada)
            return Conflict(new { mensaje = "Esta cita ya fue liquidada. No se pueden agregar cargos nuevos." });

        if (!cita.RequiereFarmacia)
            return Conflict(new { mensaje = "Esta cita no quedo marcada como que necesita medicamentos." });

        if (cita.Estado != 2)
            return Conflict(new { mensaje = "La consulta de esta cita ya esta cerrada. Un medicamento posterior debe recetarse desde una remision nueva." });

        var receta = new DespachoFarmacia
        {
            CitaId = dto.CitaId,
            Medicamento = dto.Medicamento.Trim(),
            Cantidad = dto.Cantidad,
            TiempoAplicacion = string.IsNullOrWhiteSpace(dto.TiempoAplicacion)
                ? null
                : dto.TiempoAplicacion.Trim()
        };

        int id = await _repo.CrearRecetaAsync(receta);
        var creada = await _repo.ObtenerDespachoPorIdAsync(id);

        return CreatedAtAction(nameof(GetDespacho), new { id }, creada);
    }

    /// <summary>
    /// Farmacia entrega el medicamento recetado y fija su precio.
    ///
    /// Es el momento en que el cargo existe: hasta aqui la receta valia
    /// cero y la cita seguia esperando a farmacia. Al entregar, el
    /// repositorio evalua si la bitacora ya puede cerrarse.
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.Procesamiento)]
    [HttpPut("despachos/{id:int}/entrega")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutEntrega(int id, EntregaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var despacho = await _repo.ObtenerDespachoPorIdAsync(id);
        if (despacho is null)
            return NotFound(new { mensaje = $"No existe una receta con Id {id}." });

        // Una entrega no se repite. Si hay que dar mas medicamento, se
        // receta de nuevo: asi queda constancia de las dos entregas.
        if (despacho.Estado == "Despachado")
            return Conflict(new { mensaje = "Este medicamento ya fue entregado. Para entregar mas hay que recetarlo de nuevo." });

        if (await _repo.CitaLiquidadaAsync(despacho.CitaId))
            return Conflict(new { mensaje = "La cita asociada ya fue liquidada. No se pueden modificar sus costos." });

        await _repo.DespacharAsync(id, dto.CostoUnitario);
        return NoContent();
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
