using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

/// <summary>
/// Lo que la pantalla ve de un usuario.
///
/// No lleva PasswordHash ni ningun campo derivado de el. El hash no es
/// un dato de pantalla: aunque BCrypt no sea reversible, publicarlo
/// permite atacarlo con diccionario sin tocar el servidor. Lo unico que
/// la pantalla necesita saber es si la contrasena ya quedo definida, y
/// eso cabe en un booleano.
/// </summary>
public class UsuarioDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Rol es el valor que guarda la base; RolEtiqueta es como se lee en
    // pantalla. Mando los dos para que la tabla muestre uno y el select
    // seleccione el otro sin tener que traducir en JavaScript.
    public string Rol { get; set; } = string.Empty;
    public string RolEtiqueta { get; set; } = string.Empty;

    public int? EspecialidadId { get; set; }
    public string? Especialidad { get; set; }

    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }

    /// <summary>
    /// Falso mientras el usuario siga con la contrasena de marcador que
    /// dejo el script de semilla. Lo calcula el repositorio mirando el
    /// prefijo del hash, no el hash completo.
    /// </summary>
    public bool TieneContrasenaDefinida { get; set; }

    /// <summary>
    /// Remisiones y citas en las que el usuario aparece como autor o
    /// como especialista. Es el dato que justifica la baja logica: si
    /// tiene historial, borrarlo dejaria registros clinicos apuntando a
    /// un usuario inexistente.
    /// </summary>
    public int RegistrosAsociados { get; set; }
}

/// <summary>
/// Alta de usuario. La contrasena viaja en claro dentro del cuerpo
/// (sobre HTTPS en produccion) y se hashea en el controlador antes de
/// llegar al repositorio. En ningun momento se guarda tal cual.
/// </summary>
public class UsuarioCreateDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [MaxLength(50)]
    public string Rol { get; set; } = string.Empty;

    // Obligatoria solo cuando el rol es Especialista. No lo puedo
    // expresar con [Required] porque depende de otro campo, asi que la
    // regla la valida el controlador y la respalda el CHECK de la base.
    public int? EspecialidadId { get; set; }

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contrasena debe tener al menos 8 caracteres.")]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Edicion de usuario. Deliberadamente sin contrasena: cambiar los
/// datos de alguien y restablecerle el acceso son dos operaciones
/// distintas, y mezclarlas haria que cada guardado del formulario
/// pudiera pisar la contrasena por descuido.
/// </summary>
public class UsuarioUpdateDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [MaxLength(50)]
    public string Rol { get; set; } = string.Empty;

    public int? EspecialidadId { get; set; }
}

/// <summary>Restablecimiento de contrasena desde el panel.</summary>
public class CambiarPasswordDto
{
    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contrasena debe tener al menos 8 caracteres.")]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Un rol y como se escribe en pantalla. La pantalla pide esta lista
/// en lugar de traer los cinco valores escritos a mano en el HTML.
/// </summary>
public class RolDto
{
    public string Valor { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;

    /// <summary>
    /// Cierto solo para Especialista. Es lo que la pantalla usa para
    /// decidir si habilita el select de especialidad, en vez de
    /// comparar contra la cadena "Especialista" en JavaScript.
    /// </summary>
    public bool RequiereEspecialidad { get; set; }
}
