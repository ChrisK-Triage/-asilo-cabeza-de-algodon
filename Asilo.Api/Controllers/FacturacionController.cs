using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Calculadora financiera, facturacion y pagos (RF-06, RF-07 y RF-10).
/// </summary>
[ApiController]
[Route("api/facturacion")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.SoloAdministrador)]
public class FacturacionController : ControllerBase
{
    private readonly IFacturaRepository _repo;
    private readonly IResidenteRepository _residentes;

    // Los metodos de pago que admite la restriccion CHECK de la tabla.
    // Los valido aqui para devolver un 400 con el mensaje util en lugar
    // de dejar que falle la restriccion y salga un 500 ilegible.
    private static readonly string[] MetodosValidos =
        { "Efectivo", "Transferencia", "Cheque", "Deposito" };

    public FacturacionController(IFacturaRepository repo, IResidenteRepository residentes)
    {
        _repo = repo;
        _residentes = residentes;
    }

    // ==================== CALCULO ====================

    /// <summary>
    /// Calcula lo que debe pagar un residente en un periodo, sin emitir
    /// nada. Devuelve el desglose completo para que el administrador vea
    /// de donde sale cada monto antes de confirmar.
    /// </summary>
    [HttpGet("calculo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalculoPeriodoDto>> GetCalculo(
        [FromQuery] int residenteId, [FromQuery] int anio, [FromQuery] int mes)
    {
        if (mes < 1 || mes > 12)
            return BadRequest(new { mensaje = "El mes debe estar entre 1 y 12." });

        var calculo = await _repo.CalcularPeriodoAsync(residenteId, anio, mes);

        return calculo is null
            ? NotFound(new { mensaje = $"No existe un residente con Id {residenteId}." })
            : Ok(calculo);
    }

    // ==================== EMISION ====================

    /// <summary>
    /// Emite la factura del periodo y marca como liquidadas las citas
    /// que la componen (RF-06).
    /// </summary>
    [HttpPost("facturas")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacturaDto>> PostFactura(EmitirFacturaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _residentes.ExisteAsync(dto.ResidenteId))
            return BadRequest(new { mensaje = $"No existe un residente activo con Id {dto.ResidenteId}." });

        // Aqui habia un candado que rechazaba cualquier segunda factura
        // del mismo periodo. Protegia de cobrar la estancia dos veces,
        // pero dejaba sin cobrar toda cita cerrada despues del corte: el
        // mes ya no admitia otra factura y esos cargos se quedaban en el
        // aire sin ninguna operacion que los pudiera liquidar.
        //
        // La regla correcta no es "una factura por periodo" sino "una
        // cuota de estancia por periodo". Planteada asi, la garantiza el
        // indice UX_Facturas_CuotaUnicaPorPeriodo, y el calculo pone la
        // cuota en cero en cuanto detecta que el periodo ya se facturo.
        var calculo = await _repo.CalcularPeriodoAsync(dto.ResidenteId, dto.Anio, dto.Mes);
        if (calculo is null)
            return BadRequest(new { mensaje = $"No existe un residente con Id {dto.ResidenteId}." });

        if (calculo.ConvenioId is null)
            return Conflict(new { mensaje = "No hay un convenio vigente con la fundacion. Sin convenio no se puede determinar el descuento." });

        // El mensaje distingue los dos casos porque son situaciones
        // distintas: un periodo sin movimiento, o uno ya cobrado al que
        // no le ha llegado nada nuevo desde el cierre.
        if (!calculo.HayQueFacturar)
            return Conflict(new
            {
                mensaje = calculo.EsComplementaria
                    ? $"El periodo {dto.Mes:00}/{dto.Anio} ya se cobro en la factura #{calculo.FacturaExistenteId} " +
                      "y no hay citas cerradas despues de esa emision."
                    : "No hay nada que facturar en ese periodo: el residente no tiene citas liquidables ni cuota mensual."
            });

        int id;
        try
        {
            id = await _repo.EmitirAsync(calculo);
        }
        catch (InvalidOperationException ex)
        {
            // Carrera entre dos emisiones del mismo periodo. Los datos
            // eran validos al calcularlos; lo que cambio mientras tanto
            // fue el estado del sistema, asi que es 409 y no 400.
            return Conflict(new { mensaje = ex.Message });
        }

        var emitida = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetFactura), new { id }, emitida);
    }

    /// <summary>Obtiene una factura por su Id.</summary>
    [HttpGet("facturas/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacturaDto>> GetFactura(int id)
    {
        var factura = await _repo.ObtenerPorIdAsync(id);

        return factura is null
            ? NotFound(new { mensaje = $"No existe una factura con Id {id}." })
            : Ok(factura);
    }

    /// <summary>Lista las facturas de un residente.</summary>
    [HttpGet("facturas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FacturaDto>>> GetFacturas([FromQuery] int residenteId)
        => Ok(await _repo.ObtenerPorResidenteAsync(residenteId));

    // ==================== PAGOS ====================

    /// <summary>Lista los abonos recibidos sobre una factura.</summary>
    [HttpGet("facturas/{id:int}/pagos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PagoDto>>> GetPagos(int id)
    {
        if (await _repo.ObtenerPorIdAsync(id) is null)
            return NotFound(new { mensaje = $"No existe una factura con Id {id}." });

        return Ok(await _repo.ObtenerPagosAsync(id));
    }

    /// <summary>
    /// Registra un abono sobre una factura (RF-07). Un pago parcial deja
    /// la factura en saldo pendiente; uno que cubre el total la liquida.
    /// </summary>
    [HttpPost("facturas/{id:int}/pagos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacturaDto>> PostPago(int id, PagoCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var factura = await _repo.ObtenerPorIdAsync(id);
        if (factura is null)
            return NotFound(new { mensaje = $"No existe una factura con Id {id}." });

        if (!MetodosValidos.Contains(dto.MetodoPago))
            return BadRequest(new { mensaje = $"Metodo de pago no valido. Admitidos: {string.Join(", ", MetodosValidos)}." });

        // 409 y no 400: el monto esta bien formado, lo que no tiene
        // sentido es cobrarle a una factura que ya no debe nada.
        if (factura.Saldo <= 0)
            return Conflict(new { mensaje = "Esta factura ya esta liquidada. No admite abonos nuevos." });

        // Un abono mayor al saldo dejaria la cuenta en negativo y el
        // asilo debiendole al familiar. Si se recibio de mas, se
        // devuelve el vuelto; no se registra como pago.
        if (dto.MontoRecibido > factura.Saldo)
            return Conflict(new
            {
                mensaje = $"El abono de {dto.MontoRecibido:N2} supera el saldo pendiente de {factura.Saldo:N2}."
            });

        await _repo.RegistrarPagoAsync(id, dto.MontoRecibido, dto.MetodoPago, dto.RegistradoPor);

        // Devuelvo la factura actualizada: el cliente necesita el saldo
        // nuevo, no el que tenia antes del abono.
        var actualizada = await _repo.ObtenerPorIdAsync(id);
        return CreatedAtAction(nameof(GetFactura), new { id }, actualizada);
    }

    // ==================== ESTADO DE CUENTA ====================

    /// <summary>
    /// Estado de cuenta del residente: total facturado, total cobrado y
    /// saldo pendiente, con el detalle de cada factura (RF-10).
    /// </summary>
    [HttpGet("estado-cuenta/{residenteId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstadoCuentaDto>> GetEstadoCuenta(int residenteId)
    {
        var estado = await _repo.ObtenerEstadoCuentaAsync(residenteId);

        return estado is null
            ? NotFound(new { mensaje = $"No existe un residente con Id {residenteId}." })
            : Ok(estado);
    }
}
