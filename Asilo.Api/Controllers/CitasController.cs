using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

[ApiController]
[Route("api/citas")]
[Produces("application/json")]
[Authorize(Roles = RolesUsuario.Permiso.AgendaCitas)]
public class CitasController : ControllerBase
{
    private readonly ICitaRepository _repo;
    private readonly IRemisionRepository _repoRemision;

    public CitasController(ICitaRepository repo, IRemisionRepository repoRemision)
    {
        _repo = repo;
        _repoRemision = repoRemision;
    }

    /// <summary>Lista las citas. Filtros opcionales por estado y especialista.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CitaDto>>> Get(
        [FromQuery] EstadoCita? estado,
        [FromQuery] int? especialistaId)
    {
        // Un especialista solo ve lo suyo, y el filtro lo impone el
        // servidor a partir del token: si lo dejara en manos del
        // parametro, bastaria cambiar el numero en la direccion para
        // leer la agenda y los diagnosticos de otro especialista.
        if (User.IsInRole(RolesUsuario.Especialista)
            && !User.IsInRole(RolesUsuario.Administrador))
        {
            especialistaId = UsuarioDelToken();
        }

        var citas = await _repo.ObtenerTodasAsync(estado, especialistaId);

        return Ok(citas.Select(SegunElRol));
    }

    /// <summary>Id del usuario que firma la peticion, tomado del token.</summary>
    private int UsuarioDelToken()
        => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int id)
            ? id
            : 0;

    /// <summary>
    /// Recorta los campos clinicos de la cita segun quien pregunta.
    ///
    /// La fundacion arma la agenda: necesita la sintomatologia para saber
    /// que especialidad asignar, pero no el diagnostico, que es el
    /// hallazgo de la consulta y no cambia nada de su trabajo.
    ///
    /// El tecnico no necesita ninguno de los dos: registra un examen o un
    /// despacho contra una cita, y para eso le basta el residente, la
    /// especialidad y el estado.
    ///
    /// Mismo criterio que en residentes: si un dato no debe verse, no
    /// sale del servidor. Una pantalla que lo recibe y no lo dibuja
    /// igual lo tiene.
    /// </summary>
    private CitaDto SegunElRol(CitaDto cita)
    {
        if (User.IsInRole(RolesUsuario.TecnicoLabFarmacia))
        {
            cita.Diagnostico = null;
            cita.Sintomatologia = string.Empty;
        }
        else if (User.IsInRole(RolesUsuario.Fundacion))
        {
            cita.Diagnostico = null;
        }

        return cita;
    }

    /// <summary>Obtiene una cita por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CitaDto>> GetById(int id)
    {
        var cita = await _repo.ObtenerPorIdAsync(id);

        return cita is null
            ? NotFound(new { mensaje = $"No existe una cita con Id {id}." })
            : Ok(SegunElRol(cita));
    }

    /// <summary>Catalogo de especialistas para alimentar el select de la consola.</summary>
    [HttpGet("especialistas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EspecialistaDto>>> GetEspecialistas()
        => Ok(await _repo.ObtenerEspecialistasAsync());

    /// <summary>
    /// La fundacion asigna especialista y horario a una remision pendiente (RF-03).
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.Coordinacion)]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CitaDto>> Post(AsignarCitaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // 1. La remision debe existir
        var remision = await _repoRemision.ObtenerPorIdAsync(dto.RemisionId);
        if (remision is null)
            return BadRequest(new { mensaje = $"La remision con Id {dto.RemisionId} no existe." });

        // 2. Solo se asignan remisiones pendientes. Una cancelada o ya
        //    programada no deberia volver a entrar al flujo.
        if (remision.Estado != "Pendiente")
            return Conflict(new { mensaje = $"La remision ya esta en estado {remision.Estado}. Solo se asignan remisiones pendientes." });

        // 3. Relacion 1 a 0..1: una remision origina como maximo una cita
        if (await _repo.RemisionYaTieneCitaAsync(dto.RemisionId))
            return Conflict(new { mensaje = "Esta remision ya tiene una cita asignada." });

        // 4. El especialista debe existir y tener ese rol
        var especialistas = await _repo.ObtenerEspecialistasAsync();
        var especialista = especialistas.FirstOrDefault(e => e.Id == dto.EspecialistaId);
        if (especialista is null)
            return BadRequest(new { mensaje = $"El especialista con Id {dto.EspecialistaId} no existe o no esta activo." });

        // 5. La especialidad del especialista debe ser la que pidio el
        //    medico general. Un cardiologo atendiendo una remision de
        //    neurologia invalida la consulta y el cobro que sale de ella.
        //
        //    Ahora es una comparacion de enteros. Antes habia que
        //    comparar dos cadenas quitando tildes y mayusculas, porque
        //    la especialidad era texto libre en dos tablas distintas;
        //    con el catalogo, o es el mismo Id o no lo es.
        if (especialista.EspecialidadId != remision.EspecialidadId)
            return Conflict(new
            {
                mensaje = $"{especialista.NombreCompleto} es de {especialista.Especialidad}, " +
                          $"y la remision solicita {remision.EspecialidadSolicitada}."
            });

        // 6. Choque de horario. Es la excepcion A1 del caso de uso CU-02.
        if (await _repo.HorarioOcupadoAsync(dto.EspecialistaId, dto.BloqueHorario))
            return Conflict(new { mensaje = $"{especialista.NombreCompleto} ya tiene una cita asignada en ese horario." });

        // 7. La cita no puede agendarse en el pasado
        if (dto.BloqueHorario < DateTime.Now)
            return BadRequest(new { mensaje = "El bloque horario no puede ser anterior a la fecha actual." });

        var cita = new CitaFundacion
        {
            RemisionId = dto.RemisionId,
            EspecialistaId = dto.EspecialistaId,
            BloqueHorario = dto.BloqueHorario,
            CostoConsulta = dto.CostoConsulta,
            Estado = EstadoCita.Programada,
            Liquidada = false
        };

        int id = await _repo.AsignarAsync(cita);
        var creada = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creada);
    }

    /// <summary>
    /// El especialista registra el diagnostico de la consulta (RF-04).
    /// </summary>
    [Authorize(Roles = RolesUsuario.Permiso.Consulta)]
    [HttpPut("{id:int}/diagnostico")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PutDiagnostico(int id, DiagnosticoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var cita = await _repo.ObtenerPorIdAsync(id);
        if (cita is null)
            return NotFound(new { mensaje = $"No existe una cita con Id {id}." });

        // Un diagnostico ya asentado no se sobrescribe: el expediente
        // clinico es un registro historico, no un borrador.
        if (!string.IsNullOrWhiteSpace(cita.Diagnostico))
            return Conflict(new { mensaje = "Esta cita ya tiene un diagnostico registrado. Una correccion debe asentarse como nota nueva." });

        await _repo.RegistrarDiagnosticoAsync(
            id, dto.Diagnostico.Trim(), dto.RequiereLaboratorio, dto.RequiereFarmacia);

        return NoContent();
    }

}
