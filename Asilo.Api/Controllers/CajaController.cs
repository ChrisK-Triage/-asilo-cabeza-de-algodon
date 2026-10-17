using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Donaciones y gastos operativos (RF-07).
///
/// El requerimiento pide registrar tres cosas: donaciones de empresas,
/// gobierno y particulares; las cuotas de los familiares; y los egresos
/// operativos, incluido lo que se le paga a la fundacion. Las cuotas ya
/// estaban en FacturacionController, que es donde corresponden porque
/// nacen de una factura. Lo que falta es lo otro dos, que es lo que
/// este controlador cubre.
/// </summary>
[ApiController]
[Route("api/caja")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
public class CajaController : ControllerBase
{
    private readonly ICajaRepository _repo;

    /// <summary>
    /// Hasta cuantos dias hacia atras se puede fechar un movimiento.
    ///
    /// Treinta deja ponerse al dia con lo del mes —una donacion que
    /// llego la semana pasada y nadie capturo— pero impide fechar algo
    /// dentro de un periodo que ya se reporto. Un movimiento que
    /// aparece de pronto en un mes cerrado cambia cifras que alguien ya
    /// leyo, y el reporte de ingresos y egresos de ese periodo deja de
    /// coincidir con el que se imprimio.
    /// </summary>
    private const int DiasHaciaAtrasPermitidos = 30;

    public CajaController(ICajaRepository repo) => _repo = repo;

    /// <summary>
    /// La regla de fechas, que donaciones y gastos comparten. Devuelve
    /// null cuando la fecha sirve y la respuesta de error cuando no.
    /// </summary>
    private ActionResult? FechaFueraDeRango(DateTime fecha, string queEs)
    {
        var limite = DateTime.Today.AddDays(-DiasHaciaAtrasPermitidos);

        if (fecha.Date > DateTime.Today)
            return BadRequest(new { mensaje = $"La fecha {queEs} no puede ser futura." });

        if (fecha.Date < limite)
            return BadRequest(new
            {
                mensaje = $"La fecha {queEs} no puede ser anterior al {limite:dd/MM/yyyy}. " +
                          $"Solo se admiten movimientos de los ultimos {DiasHaciaAtrasPermitidos} dias."
            });

        return null;
    }

    // ==================== DONACIONES ====================

    /// <summary>Lista las donaciones recibidas. Filtro opcional por rango de fechas.</summary>
    [HttpGet("donaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DonacionDto>>> GetDonaciones(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => Ok(await _repo.ObtenerDonacionesAsync(desde, hasta));

    /// <summary>Obtiene una donacion por su Id.</summary>
    [HttpGet("donaciones/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonacionDto>> GetDonacion(int id)
    {
        var donacion = await _repo.ObtenerDonacionPorIdAsync(id);

        return donacion is null
            ? NotFound(new { mensaje = $"No existe una donacion con Id {id}." })
            : Ok(donacion);
    }

    /// <summary>Registra una donacion.</summary>
    [HttpPost("donaciones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DonacionDto>> PostDonacion(DonacionCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (FechaFueraDeRango(dto.Fecha, "de la donacion") is { } fueraDeRango)
            return fueraDeRango;

        var donacion = new Donacion
        {
            NombreDonante = dto.NombreDonante.Trim(),
            Tipo = dto.Tipo,
            Monto = dto.Monto,
            Fecha = dto.Fecha.Date,
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim()
        };

        int id = await _repo.CrearDonacionAsync(donacion);
        var creada = await _repo.ObtenerDonacionPorIdAsync(id);

        return CreatedAtAction(nameof(GetDonacion), new { id }, creada);
    }

    /// <summary>Elimina una donacion capturada por error.</summary>
    [HttpDelete("donaciones/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDonacion(int id)
        => await _repo.EliminarDonacionAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe una donacion con Id {id}." });

    // ==================== GASTOS OPERATIVOS ====================

    /// <summary>
    /// Las categorias que acepta la base. La pantalla llena el
    /// desplegable con esto en lugar de traerlas escritas en el HTML,
    /// para que no se desincronicen de CK_Gastos_Categoria.
    /// </summary>
    [HttpGet("categorias")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CategoriaGastoDto>> GetCategorias()
        => Ok(CategoriasGasto.Todas.Select(c => new CategoriaGastoDto
        {
            Valor = c,
            Etiqueta = CategoriasGasto.Etiqueta(c)
        }));

    /// <summary>Lista los gastos. Filtros opcionales por fechas y categoria.</summary>
    [HttpGet("gastos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GastoDto>>> GetGastos(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? categoria)
        => Ok(await _repo.ObtenerGastosAsync(desde, hasta, categoria));

    /// <summary>Obtiene un gasto por su Id.</summary>
    [HttpGet("gastos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GastoDto>> GetGasto(int id)
    {
        var gasto = await _repo.ObtenerGastoPorIdAsync(id);

        return gasto is null
            ? NotFound(new { mensaje = $"No existe un gasto con Id {id}." })
            : Ok(gasto);
    }

    /// <summary>Registra un egreso operativo.</summary>
    [HttpPost("gastos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GastoDto>> PostGasto(GastoCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Valido contra la misma lista que alimenta el desplegable. Sin
        // esto, una categoria invalida choca con CK_Gastos_Categoria y
        // sale un 500 con el texto de la restriccion.
        if (!CategoriasGasto.EsValida(dto.Categoria))
            return BadRequest(new
            {
                mensaje = $"Categoria no valida. Admitidas: {string.Join(", ", CategoriasGasto.Todas)}."
            });

        if (FechaFueraDeRango(dto.Fecha, "del gasto") is { } fueraDeRango)
            return fueraDeRango;

        var gasto = new GastoOperativo
        {
            Concepto = dto.Concepto.Trim(),
            Categoria = dto.Categoria,
            Monto = dto.Monto,
            Fecha = dto.Fecha.Date
        };

        int id = await _repo.CrearGastoAsync(gasto);
        var creado = await _repo.ObtenerGastoPorIdAsync(id);

        return CreatedAtAction(nameof(GetGasto), new { id }, creado);
    }

    /// <summary>Elimina un gasto capturado por error.</summary>
    [HttpDelete("gastos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGasto(int id)
        => await _repo.EliminarGastoAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un gasto con Id {id}." });
}
