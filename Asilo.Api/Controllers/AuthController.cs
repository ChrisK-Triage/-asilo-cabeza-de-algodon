using System.Security.Claims;
using Asilo.Api.DTOs;
using Asilo.Api.Repositories;
using Asilo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asilo.Api.Controllers;

/// <summary>
/// Inicio de sesion (RNF-02).
///
/// El analisis planteaba autenticacion por cookies con ASP.NET Core
/// Identity. Al pasar a Web API REST (CA-02) la sesion se resuelve con
/// un token firmado que el cliente guarda y manda en cada peticion.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoRepository _catalogos;
    private readonly IServicioToken _token;

    public AuthController(IUsuarioRepository usuarios, ICatalogoRepository catalogos, IServicioToken token)
    {
        _usuarios = usuarios;
        _catalogos = catalogos;
        _token = token;
    }

    /// <summary>
    /// Valida credenciales y devuelve el token de sesion.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SesionDto>> PostLogin(LoginDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var usuario = await _usuarios.ObtenerPorEmailParaLoginAsync(dto.Email.Trim().ToLowerInvariant());

        // Un solo mensaje para "no existe", "esta dado de baja" y
        // "contrasena incorrecta". Distinguirlos le diria a cualquiera
        // que pruebe correos cuales estan registrados en el sistema.
        if (usuario is null || !VerificarPassword(dto.Password, usuario.PasswordHash))
        {
            // El caso del usuario de semilla si merece su propio
            // mensaje: no es un intento fallido sino una cuenta que
            // nunca termino de darse de alta, y sin decirlo no hay
            // forma de saber por que no entra.
            if (usuario is not null && !EsHashBCrypt(usuario.PasswordHash))
                return Conflict(new
                {
                    mensaje = "Esta cuenta todavia no tiene contrasena definida. " +
                              "Hay que asignarsela desde el panel de administracion de usuarios."
                });

            return Unauthorized(new { mensaje = "Correo o contrasena incorrectos." });
        }

        string? especialidad = usuario.EspecialidadId is null
            ? null
            : (await _catalogos.ObtenerEspecialidadPorIdAsync(usuario.EspecialidadId.Value))?.Nombre;

        return Ok(_token.CrearSesion(usuario, especialidad));
    }

    /// <summary>
    /// Devuelve quien es el portador del token. El frontend la usa al
    /// cargar una pantalla para comprobar que la sesion sigue viva: si
    /// el token vencio, responde 401 y la pantalla manda al login.
    /// </summary>
    [Authorize]
    [HttpGet("yo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioDto>> GetYo()
    {
        // El Id sale del token, no de la peticion. Es la diferencia
        // entre "quien dice ser" y "quien el servidor firmo que es".
        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(id, out int usuarioId))
            return Unauthorized(new { mensaje = "El token no identifica a ningun usuario." });

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId);

        // El token sigue siendo valido pero el usuario ya no: lo dieron
        // de baja despues de emitirlo. Un JWT no se puede revocar, asi
        // que la comprobacion contra la base es la que cierra el hueco.
        if (usuario is null || !usuario.Activo)
            return Unauthorized(new { mensaje = "La cuenta ya no esta activa." });

        return Ok(usuario);
    }

    /// <summary>
    /// BCrypt.Verify revienta si el hash no tiene su formato, y el de
    /// la semilla no lo tiene. Lo compruebo antes para que un dato
    /// heredado no se convierta en un error 500.
    /// </summary>
    private static bool VerificarPassword(string password, string hash)
        => EsHashBCrypt(hash) && BCrypt.Net.BCrypt.Verify(password, hash);

    private static bool EsHashBCrypt(string hash)
        => !string.IsNullOrEmpty(hash) && hash.StartsWith("$2");
}
