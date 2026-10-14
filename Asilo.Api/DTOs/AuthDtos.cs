using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

/// <summary>
/// Credenciales que manda la pantalla de inicio de sesion.
/// </summary>
public class LoginDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Lo que devuelve un inicio de sesion correcto.
///
/// Mando tambien el nombre y el rol para que el frontend no tenga que
/// abrir el token para pintar el encabezado y el menu. El token sigue
/// siendo la unica fuente de verdad para la API: lo que viaja aqui es
/// para mostrar, no para autorizar. Un usuario puede modificar este
/// JSON en su navegador y no gana ningun permiso, porque el servidor
/// no lo lee: lee el token, que viene firmado.
/// </summary>
public class SesionDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expira { get; set; }

    public int UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string RolEtiqueta { get; set; } = string.Empty;

    public int? EspecialidadId { get; set; }
    public string? Especialidad { get; set; }

    /// <summary>
    /// Pagina a la que lleva el inicio de sesion segun el rol. La
    /// decide el servidor y no el JavaScript para que el mapa de
    /// rol a modulo viva en un solo lugar.
    /// </summary>
    public string PaginaInicio { get; set; } = string.Empty;

    /// <summary>
    /// Archivos de navegacion que este rol puede abrir. El menu lateral
    /// se filtra con esto.
    ///
    /// Es comodidad de presentacion, no seguridad: esconder un enlace no
    /// protege nada. Lo que protege son los [Authorize] de la API, que
    /// responden 403 aunque alguien escriba la direccion a mano.
    /// </summary>
    public List<string> PaginasPermitidas { get; set; } = new();
}
