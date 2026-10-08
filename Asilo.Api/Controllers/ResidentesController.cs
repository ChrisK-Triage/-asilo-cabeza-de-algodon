using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/residentes")]
[Produces("application/json")]
public class ResidentesController : ControllerBase
{
    private readonly IResidenteRepository _repo;

    // El contenedor de inyeccion de dependencias arma el repositorio
    // y lo entrega aqui. Nunca instancio con new.
    public ResidentesController(IResidenteRepository repo) => _repo = repo;

    /// <summary>Lista los residentes activos del asilo.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ResidenteDto>>> Get()
        => Ok(await _repo.ObtenerTodosAsync());

    /// <summary>Obtiene el expediente de un residente por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResidenteDto>> GetById(int id)
    {
        var residente = await _repo.ObtenerPorIdAsync(id);

        return residente is null
            ? NotFound(new { mensaje = $"No existe un residente con Id {id}." })
            : Ok(residente);
    }

    /// <summary>Registra un residente nuevo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResidenteDto>> Post(ResidenteCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.FechaNacimiento >= DateTime.Today)
            return BadRequest(new { mensaje = "La fecha de nacimiento debe ser anterior a hoy." });

        var residente = new Residente
        {
            Nombres = dto.Nombres.Trim(),
            Apellidos = dto.Apellidos.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            Psicopatologias = dto.Psicopatologias,
            MedicamentosCajon = dto.MedicamentosCajon,
            Activo = true
        };

        int id = await _repo.CrearAsync(residente);
        var creado = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creado);
    }

    /// <summary>Actualiza los datos de un residente existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(int id, ResidenteCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _repo.ExisteAsync(id))
            return NotFound(new { mensaje = $"No existe un residente con Id {id}." });

        var residente = new Residente
        {
            Id = id,
            Nombres = dto.Nombres.Trim(),
            Apellidos = dto.Apellidos.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            Psicopatologias = dto.Psicopatologias,
            MedicamentosCajon = dto.MedicamentosCajon
        };

        await _repo.ActualizarAsync(residente);
        return NoContent();
    }

    /// <summary>Da de baja a un residente (baja logica, no elimina el expediente).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => await _repo.DesactivarAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un residente activo con Id {id}." });
}
