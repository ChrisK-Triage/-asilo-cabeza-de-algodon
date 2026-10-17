using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Asilo.Api.Services;

/// <summary>
/// Fabrica el token JWT y decide a que modulos entra cada rol.
///
/// El mapa de rol a paginas vive aqui y no en el JavaScript porque el
/// mismo criterio tiene que servir para dos cosas: filtrar el menu y
/// decidir a donde cae el usuario al entrar. Teniendolo en el servidor
/// se escribe una vez.
/// </summary>
public class ServicioToken : IServicioToken
{
    private readonly IConfiguration _config;

    public ServicioToken(IConfiguration config) => _config = config;

    public SesionDto CrearSesion(Usuario usuario, string? especialidad)
    {
        var paginas = PaginasDe(usuario.Rol);
        int minutos = _config.GetValue<int?>("Jwt:MinutosVigencia") ?? 480;
        var expira = DateTime.UtcNow.AddMinutes(minutos);

        return new SesionDto
        {
            Token = Firmar(usuario, expira),
            Expira = expira,
            UsuarioId = usuario.Id,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Rol = usuario.Rol,
            RolEtiqueta = RolesUsuario.Etiqueta(usuario.Rol),
            EspecialidadId = usuario.EspecialidadId,
            Especialidad = especialidad,
            PaginaInicio = paginas.FirstOrDefault() ?? "login.html",
            PaginasPermitidas = paginas
        };
    }

    private string Firmar(Usuario usuario, DateTime expira)
    {
        string llave = _config["Jwt:Llave"]
            ?? throw new InvalidOperationException("Falta 'Jwt:Llave' en appsettings.json.");

        // HMAC-SHA256 exige al menos 256 bits de llave. Lo compruebo
        // aqui porque el error que da la libreria sin esto no dice que
        // el problema es el largo de la cadena.
        if (Encoding.UTF8.GetByteCount(llave) < 32)
            throw new InvalidOperationException("'Jwt:Llave' debe tener al menos 32 caracteres.");

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llave)),
            SecurityAlgorithms.HmacSha256);

        // Las afirmaciones del token. El rol es la que leen los
        // [Authorize(Roles = ...)] de los controladores.
        var afirmaciones = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol)
        };

        // El panel del especialista filtra sus citas con esto, de modo
        // que no necesita mandar su propio Id y nadie puede pedir las
        // citas de otro cambiando un parametro.
        if (usuario.EspecialidadId is not null)
            afirmaciones.Add(new Claim("especialidadId", usuario.EspecialidadId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Emisor"],
            audience: _config["Jwt:Audiencia"],
            claims: afirmaciones,
            notBefore: DateTime.UtcNow,
            expires: expira,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Que pantallas abre cada rol. El primer elemento es la pagina a
    /// la que cae al entrar.
    ///
    /// Cerrar sesion no figura porque esta disponible siempre; el
    /// frontend la deja fija.
    /// </summary>
    private static List<string> PaginasDe(string rol) => rol switch
    {
        RolesUsuario.Administrador => new()
        {
            "panel-clinico.html", "crear-remision.html", "consola-fundacion.html",
            "panel-laboratorio.html", "panel-farmacia.html", "panel-especialista.html",
            "estado-cuenta.html", "panel-caja.html", "panel-reportes.html",
            "admin-usuarios.html", "panel-notificaciones.html"
        },

        RolesUsuario.MedicoGeneral => new()
        {
            "panel-clinico.html", "crear-remision.html"
        },

        // La fundacion coordina la agenda. No entra al expediente: su
        // trabajo es asignar especialista y horario, y para eso le basta
        // la sintomatologia que trae la remision.
        RolesUsuario.Fundacion => new()
        {
            "consola-fundacion.html"
        },

        RolesUsuario.Especialista => new()
        {
            "panel-especialista.html"
        },

        RolesUsuario.TecnicoLabFarmacia => new()
        {
            "panel-laboratorio.html", "panel-farmacia.html"
        },

        _ => new()
    };
}
