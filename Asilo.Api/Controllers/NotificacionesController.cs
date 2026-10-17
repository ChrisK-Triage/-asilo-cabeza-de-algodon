using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Asilo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Bandeja de avisos a la familia (RF-08).
///
/// Es de solo lectura a proposito. La cola no se edita a mano: los
/// mensajes los escribe la operacion que los origina, dentro de su
/// transaccion, y los marca el despachador. Un endpoint que permitiera
/// darlos por entregados sin haberlos mandado convertiria el registro en
/// algo que no se puede creer.
/// </summary>
[ApiController]
[Route("api/notificaciones")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
public class NotificacionesController : ControllerBase
{
    private readonly IOutboxRepository _repo;
    private readonly IConfiguration _config;

    private static readonly string[] SituacionesValidas = { "Pendiente", "Entregado", "Fallido" };

    public NotificacionesController(IOutboxRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    /// <summary>
    /// Lista la bandeja. Filtro opcional por situacion.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<OutboxMensajeDto>>> Get(
        [FromQuery] string? situacion)
    {
        if (situacion is not null && !SituacionesValidas.Contains(situacion))
            return BadRequest(new
            {
                mensaje = $"Situacion no valida. Admitidas: {string.Join(", ", SituacionesValidas)}."
            });

        return Ok(await _repo.ObtenerAsync(situacion, OpcionesNotificaciones.TopeIntentos(_config)));
    }

    /// <summary>Cuantos avisos hay pendientes, entregados y fallidos.</summary>
    [HttpGet("resumen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumenOutboxDto>> GetResumen()
        => Ok(await _repo.ObtenerResumenAsync(OpcionesNotificaciones.TopeIntentos(_config)));
}
