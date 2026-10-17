using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Reportes del sistema (RF-09).
///
/// Los cinco que nombra el requerimiento: historial clinico, costos por
/// cita, ingresos y egresos, pagos a la fundacion, y examenes e insumos
/// aplicados por residente y rango de fechas.
///
/// Devuelven datos, no PDF. El documento lo arma la pantalla y el
/// navegador lo imprime, que es la desviacion que quedo registrada:
/// el analisis proponia generar el PDF en el servidor con QuestPDF.
/// </summary>
[ApiController]
[Route("api/reportes")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
public class ReportesController : ControllerBase
{
    private readonly IReporteRepository _repo;

    public ReportesController(IReporteRepository repo) => _repo = repo;

    /// <summary>
    /// Un rango invertido no devuelve nada y parece que no hay datos.
    /// Mejor decirlo.
    /// </summary>
    private ActionResult? RangoInvalido(DateTime? desde, DateTime? hasta)
        => desde is not null && hasta is not null && desde > hasta
            ? BadRequest(new { mensaje = "La fecha inicial no puede ser posterior a la final." })
            : null;

    /// <summary>Historial clinico completo de un residente.</summary>
    [HttpGet("historial-clinico/{residenteId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReporteHistorialDto>> GetHistorial(
        int residenteId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (RangoInvalido(desde, hasta) is { } error) return error;

        var reporte = await _repo.HistorialClinicoAsync(residenteId, desde, hasta);

        return reporte is null
            ? NotFound(new { mensaje = $"No existe un residente con Id {residenteId}." })
            : Ok(reporte);
    }

    /// <summary>Costos de cada cita, desglosados por consulta, laboratorio y farmacia.</summary>
    [HttpGet("costos-por-cita")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteCostosCitaDto>> GetCostosPorCita(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (RangoInvalido(desde, hasta) is { } error) return error;

        return Ok(await _repo.CostosPorCitaAsync(desde, hasta));
    }

    /// <summary>Movimientos de caja del periodo, con el resultado neto.</summary>
    [HttpGet("ingresos-egresos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteIngresosEgresosDto>> GetIngresosEgresos(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (RangoInvalido(desde, hasta) is { } error) return error;

        return Ok(await _repo.IngresosEgresosAsync(desde, hasta));
    }

    /// <summary>Lo adeudado a la fundacion contra lo que se le ha pagado.</summary>
    [HttpGet("pagos-fundacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportePagosFundacionDto>> GetPagosFundacion(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (RangoInvalido(desde, hasta) is { } error) return error;

        return Ok(await _repo.PagosALaFundacionAsync(desde, hasta));
    }

    /// <summary>
    /// Examenes e insumos aplicados. Sin residenteId sale el consolidado
    /// de todos.
    /// </summary>
    [HttpGet("examenes-insumos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReporteInsumosDto>> GetExamenesInsumos(
        [FromQuery] int? residenteId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (RangoInvalido(desde, hasta) is { } error) return error;

        return Ok(await _repo.ExamenesEInsumosAsync(residenteId, desde, hasta));
    }
}
