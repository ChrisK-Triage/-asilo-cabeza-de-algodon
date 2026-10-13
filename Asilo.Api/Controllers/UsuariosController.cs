using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Asilo.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Administracion de usuarios del sistema.
///
/// Este modulo no estaba en mis requerimientos funcionales ni en mis
/// casos de uso: el analisis daba por sentado que los usuarios existian
/// sin decir quien los crea. Mientras la tabla se llenaba desde el
/// script de semilla no se notaba, pero el sistema no se puede entregar
/// con los usuarios escritos a mano en un archivo SQL.
///
/// Las dos reglas que el analisis tampoco contemplaba y que aqui quedan
/// escritas son las de los metodos Put y Delete: no quedarse sin
/// administrador y no quitarle el rol a un especialista con citas por
/// atender. Las dos dejan el sistema en un estado del que no se sale
/// desde la aplicacion, asi que valen un 409 y no un aviso.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _repo;
    private readonly ICatalogoRepository _catalogos;

    public UsuariosController(IUsuarioRepository repo, ICatalogoRepository catalogos)
    {
        _repo = repo;
        _catalogos = catalogos;
    }

    /// <summary>Lista los usuarios del sistema.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> Get(
        [FromQuery] bool incluirInactivos = false)
        => Ok(await _repo.ObtenerTodosAsync(!incluirInactivos));

    /// <summary>Obtiene un usuario por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        var usuario = await _repo.ObtenerPorIdAsync(id);

        return usuario is null
            ? NotFound(new { mensaje = $"No existe un usuario con Id {id}." })
            : Ok(usuario);
    }

    /// <summary>
    /// Los roles que acepta el sistema, con su nombre de pantalla y si
    /// exigen especialidad. La pantalla llena el desplegable con esto en
    /// lugar de traer los cinco valores escritos en el HTML.
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<RolDto>> GetRoles()
        => Ok(RolesUsuario.Todos.Select(r => new RolDto
        {
            Valor = r,
            Etiqueta = RolesUsuario.Etiqueta(r),
            RequiereEspecialidad = r == RolesUsuario.Especialista
        }));

    /// <summary>Registra un usuario nuevo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Post(UsuarioCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        string email = dto.Email.Trim().ToLowerInvariant();

        var problema = await ValidarRolYEspecialidadAsync(dto.Rol, dto.EspecialidadId);
        if (problema is not null) return problema;

        if (await _repo.EmailRepetidoAsync(email))
            return Conflict(new { mensaje = $"Ya hay un usuario registrado con el correo {email}." });

        var usuario = new Usuario
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Email = email,
            Rol = dto.Rol,
            // Fuera del rol de especialista la especialidad se descarta
            // aunque venga en la peticion: el CHECK de la base la exige
            // nula y preferir el silencio a un 400 evita trabarle el
            // formulario a quien cambio de rol sin limpiar el select.
            EspecialidadId = dto.Rol == RolesUsuario.Especialista ? dto.EspecialidadId : null,
            // BCrypt genera la sal internamente y la guarda dentro del
            // hash. Factor de costo por omision de la libreria: 11.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Activo = true
        };

        int id = await _repo.CrearAsync(usuario);
        var creado = await _repo.ObtenerPorIdAsync(id);

        return CreatedAtAction(nameof(GetById), new { id }, creado);
    }

    /// <summary>Actualiza nombre, correo, rol y especialidad.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Put(int id, UsuarioUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var actual = await _repo.ObtenerPorIdAsync(id);
        if (actual is null)
            return NotFound(new { mensaje = $"No existe un usuario con Id {id}." });

        string email = dto.Email.Trim().ToLowerInvariant();

        var problema = await ValidarRolYEspecialidadAsync(dto.Rol, dto.EspecialidadId);
        if (problema is not null) return problema;

        if (await _repo.EmailRepetidoAsync(email, id))
            return Conflict(new { mensaje = $"El correo {email} ya lo usa otro usuario." });

        // Dejar el sistema sin administrador activo no se puede deshacer
        // desde la aplicacion: nadie podria volver a entrar a esta
        // pantalla para repararlo. Habria que corregirlo con un UPDATE
        // directo en la base, que es justo lo que este modulo vino a
        // evitar.
        bool dejaDeSerAdmin = actual.Rol == RolesUsuario.Administrador
                           && dto.Rol != RolesUsuario.Administrador;

        if (dejaDeSerAdmin && actual.Activo && !await _repo.HayOtroAdministradorActivoAsync(id))
            return Conflict(new
            {
                mensaje = $"{actual.NombreCompleto} es el unico administrador activo. " +
                          "Hay que registrar o activar otro administrador antes de cambiarle el rol."
            });

        // Un especialista con citas por atender no puede dejar de ser
        // especialista: esas citas quedarian asignadas a alguien que ya
        // no tiene la especialidad que la remision pidio.
        if (actual.Rol == RolesUsuario.Especialista && dto.Rol != RolesUsuario.Especialista)
        {
            int pendientes = await _repo.ContarCitasPendientesComoEspecialistaAsync(id);

            if (pendientes > 0)
                return Conflict(new
                {
                    mensaje = $"{actual.NombreCompleto} tiene {pendientes} " +
                              (pendientes == 1 ? "cita" : "citas") +
                              " sin cerrar. Hay que atenderlas o reasignarlas antes de cambiarle el rol."
                });
        }

        var usuario = new Usuario
        {
            Id = id,
            NombreCompleto = dto.NombreCompleto.Trim(),
            Email = email,
            Rol = dto.Rol,
            EspecialidadId = dto.Rol == RolesUsuario.Especialista ? dto.EspecialidadId : null
        };

        await _repo.ActualizarAsync(usuario);
        return NoContent();
    }

    /// <summary>
    /// Restablece la contrasena de un usuario. Operacion aparte de la
    /// edicion: cambiar el nombre de alguien no deberia poder tocarle el
    /// acceso ni por descuido.
    /// </summary>
    [HttpPut("{id:int}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutPassword(int id, CambiarPasswordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _repo.ExisteAsync(id))
            return NotFound(new { mensaje = $"No existe un usuario con Id {id}." });

        await _repo.CambiarPasswordAsync(id, BCrypt.Net.BCrypt.HashPassword(dto.Password));
        return NoContent();
    }

    /// <summary>
    /// Da de baja a un usuario. Baja logica: sus remisiones y citas
    /// siguen apuntando a el y el historial tiene que poder leerse.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var usuario = await _repo.ObtenerPorIdAsync(id);

        if (usuario is null || !usuario.Activo)
            return NotFound(new { mensaje = $"No existe un usuario activo con Id {id}." });

        if (usuario.Rol == RolesUsuario.Administrador
            && !await _repo.HayOtroAdministradorActivoAsync(id))
            return Conflict(new
            {
                mensaje = $"{usuario.NombreCompleto} es el unico administrador activo. " +
                          "Dar de baja al ultimo administrador dejaria el sistema sin quien administre usuarios."
            });

        if (usuario.Rol == RolesUsuario.Especialista)
        {
            int pendientes = await _repo.ContarCitasPendientesComoEspecialistaAsync(id);

            if (pendientes > 0)
                return Conflict(new
                {
                    mensaje = $"{usuario.NombreCompleto} tiene {pendientes} " +
                              (pendientes == 1 ? "cita" : "citas") +
                              " sin cerrar. Hay que atenderlas o reasignarlas antes de darlo de baja."
                });
        }

        await _repo.DesactivarAsync(id);
        return NoContent();
    }

    /// <summary>Vuelve a habilitar a un usuario dado de baja.</summary>
    [HttpPut("{id:int}/reactivacion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutReactivacion(int id)
        => await _repo.ReactivarAsync(id)
            ? NoContent()
            : NotFound(new { mensaje = $"No existe un usuario inactivo con Id {id}." });

    // ==================== VALIDACION COMPARTIDA ====================

    /// <summary>
    /// La regla rol-especialidad, que alta y edicion comparten. Devuelve
    /// null cuando todo esta bien y la respuesta de error cuando no.
    ///
    /// Es la misma regla que CK_Usuarios_EspecialidadRol, escrita dos
    /// veces a proposito: en la base para que ningun INSERT directo la
    /// esquive, y aqui para responder con un mensaje legible en lugar de
    /// un error 500 con el texto de la restriccion.
    /// </summary>
    private async Task<ActionResult?> ValidarRolYEspecialidadAsync(string rol, int? especialidadId)
    {
        if (!RolesUsuario.EsValido(rol))
            return BadRequest(new
            {
                mensaje = $"El rol \"{rol}\" no existe. Validos: {string.Join(", ", RolesUsuario.Todos)}."
            });

        if (rol != RolesUsuario.Especialista) return null;

        if (especialidadId is null)
            return BadRequest(new { mensaje = "Un especialista necesita una especialidad asignada." });

        var especialidad = await _catalogos.ObtenerEspecialidadPorIdAsync(especialidadId.Value);

        if (especialidad is null)
            return BadRequest(new { mensaje = $"No existe una especialidad con Id {especialidadId}." });

        // Una especialidad dada de baja sigue existiendo para leer el
        // historial, pero no para asignarla a alguien nuevo.
        if (!especialidad.Activa)
            return Conflict(new
            {
                mensaje = $"La especialidad {especialidad.Nombre} esta dada de baja. " +
                          "Hay que reactivarla en el catalogo antes de asignarla."
            });

        return null;
    }
}
