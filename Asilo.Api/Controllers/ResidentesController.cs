using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/residentes")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.LecturaClinica)]
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
    {
        var residentes = await _repo.ObtenerTodosAsync();

        return Ok(residentes.Select(SinDatosClinicosSiCorresponde));
    }

    /// <summary>Obtiene el expediente de un residente por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResidenteDto>> GetById(int id)
    {
        var residente = await _repo.ObtenerPorIdAsync(id);

        return residente is null
            ? NotFound(new { mensaje = $"No existe un residente con Id {id}." })
            : Ok(SinDatosClinicosSiCorresponde(residente));
    }

    /// <summary>
    /// Quita psicopatologias y medicamentos de cajon cuando quien
    /// pregunta es un rol que coordina o procesa, pero no trata.
    ///
    /// Es la prueba CP-12 del plan de SQA. El filtro va aqui y no en la
    /// pantalla por una razon concreta: una pantalla que recibe el dato
    /// y no lo dibuja igual lo tiene, y basta abrir la consola del
    /// navegador o llamar al endpoint directo para leerlo. Si el dato
    /// no debe verse, no debe salir del servidor.
    ///
    /// Hoy el unico rol de este grupo que llega hasta aqui es Fundacion,
    /// porque el tecnico ni siquiera pasa el [Authorize] de la clase.
    /// Dejo los dos en la lista para que la regla siga valiendo si
    /// manana se amplia el permiso de lectura.
    /// </summary>
    private ResidenteDto SinDatosClinicosSiCorresponde(ResidenteDto residente)
    {
        if (!RolesUsuario.Permiso.SinAccesoClinico.Any(User.IsInRole))
            return residente;

        residente.Psicopatologias = null;
        residente.MedicamentosCajon = null;
        return residente;
    }

    /// <summary>Registra un residente nuevo.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
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
            Dpi = string.IsNullOrWhiteSpace(dto.Dpi) ? null : dto.Dpi.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            CuotaMensual = dto.CuotaMensual,
            Psicopatologias = dto.Psicopatologias,
            MedicamentosCajon = dto.MedicamentosCajon,
            Activo = true
        };

        int id = await _repo.CrearAsync(residente);
        var creado = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creado);
    }

    /// <summary>Actualiza los datos de un residente existente.</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
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
            Dpi = string.IsNullOrWhiteSpace(dto.Dpi) ? null : dto.Dpi.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            CuotaMensual = dto.CuotaMensual,
            Psicopatologias = dto.Psicopatologias,
            MedicamentosCajon = dto.MedicamentosCajon
        };

        await _repo.ActualizarAsync(residente);
        return NoContent();
    }

    /// <summary>
    /// Actualiza unicamente los datos clinicos del expediente:
    /// psicopatologias y medicamentos de cajon (RF-01).
    ///
    /// Es un endpoint aparte del PUT completo porque son dos trabajos
    /// distintos con dos responsables distintos. Dar de alta a un
    /// residente es un acto administrativo —se pacta el DPI, la fecha de
    /// ingreso, la cuota de estancia y los contactos de la familia— y lo
    /// hace administracion. Lo que escribe el medico general son estos
    /// dos campos.
    ///
    /// Mientras hubo un solo PUT, darle al medico permiso para anotar un
    /// diagnostico le daba de paso permiso para cambiar la cuota que se
    /// le cobra a la familia. Separarlo es lo que cierra eso, y lo cierra
    /// en la sentencia SQL, no en una validacion.
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.ExpedienteClinico)]
    [HttpPut("{id:int}/clinico")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutClinico(int id, ResidenteClinicoDto dto)
    {
        if (!await _repo.ExisteAsync(id))
            return NotFound(new { mensaje = $"No existe un residente con Id {id}." });

        await _repo.ActualizarDatosClinicosAsync(
            id,
            string.IsNullOrWhiteSpace(dto.Psicopatologias) ? null : dto.Psicopatologias.Trim(),
            string.IsNullOrWhiteSpace(dto.MedicamentosCajon) ? null : dto.MedicamentosCajon.Trim());

        return NoContent();
    }

    /// <summary>Da de baja a un residente (baja logica, no elimina el expediente).</summary>
    [Authorize(Roles = RolesUsuario.Permiso.Admision)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => await _repo.DesactivarAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un residente activo con Id {id}." });
}
