using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Contactos familiares del residente.
///
/// Registrar un contacto es parte de la admision, no de la atencion
/// clinica: es a quien se le avisa de las citas y a quien se le factura.
/// Por eso la clase deja leer al medico general, pero escribir solo a
/// administracion.
/// </summary>
[ApiController]
[Route("api/familiares")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.Clinico)]
public class FamiliaresController : ControllerBase
{
    private readonly IFamiliarRepository _repo;
    private readonly IResidenteRepository _residentes;

    // Necesito los dos repositorios: el familiar no existe por si solo,
    // siempre cuelga de un residente y tengo que comprobar que ese
    // residente exista antes de guardar el contacto.
    public FamiliaresController(IFamiliarRepository repo, IResidenteRepository residentes)
    {
        _repo = repo;
        _residentes = residentes;
    }

    /// <summary>Lista los contactos registrados para un residente.</summary>
    [HttpGet("residente/{residenteId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<FamiliarDto>>> GetPorResidente(int residenteId)
    {
        if (!await _residentes.ExisteAsync(residenteId))
            return NotFound(new { mensaje = $"No existe un residente con Id {residenteId}." });

        return Ok(await _repo.ObtenerPorResidenteAsync(residenteId));
    }

    /// <summary>Obtiene un contacto por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamiliarDto>> GetById(int id)
    {
        var familiar = await _repo.ObtenerPorIdAsync(id);

        return familiar is null
            ? NotFound(new { mensaje = $"No existe un familiar con Id {id}." })
            : Ok(familiar);
    }

    /// <summary>Registra un contacto familiar para un residente.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FamiliarDto>> Post(FamiliarCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _residentes.ExisteAsync(dto.ResidenteId))
            return BadRequest(new { mensaje = $"No existe un residente activo con Id {dto.ResidenteId}." });

        // 409 y no 400: el correo esta bien escrito, lo que no permito es
        // que el mismo buzon reciba dos veces el aviso del mismo residente.
        if (await _repo.EmailRepetidoAsync(dto.ResidenteId, dto.Email.Trim()))
            return Conflict(new { mensaje = "Ese correo ya esta registrado como contacto de este residente." });

        var familiar = new Familiar
        {
            ResidenteId = dto.ResidenteId,
            Nombre = dto.Nombre.Trim(),
            Dpi = string.IsNullOrWhiteSpace(dto.Dpi) ? null : dto.Dpi.Trim(),
            Email = dto.Email.Trim(),
            Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim()
        };

        int id = await _repo.CrearAsync(familiar);
        var creado = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creado);
    }

    /// <summary>Corrige los datos de un contacto ya registrado.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Put(int id, FamiliarUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var actual = await _repo.ObtenerPorIdAsync(id);
        if (actual is null)
            return NotFound(new { mensaje = $"No existe un familiar con Id {id}." });

        // Me excluyo a mi mismo de la busqueda: si no cambio el correo,
        // el propio registro saldria como duplicado.
        if (await _repo.EmailRepetidoAsync(actual.ResidenteId, dto.Email.Trim(), id))
            return Conflict(new { mensaje = "Ese correo ya esta registrado como contacto de este residente." });

        dto.Nombre = dto.Nombre.Trim();
        dto.Dpi = string.IsNullOrWhiteSpace(dto.Dpi) ? null : dto.Dpi.Trim();
        dto.Email = dto.Email.Trim();
        dto.Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        await _repo.ActualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Elimina un contacto familiar.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => await _repo.EliminarAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un familiar con Id {id}." });
}
