namespace Asilo.Api.Models;

/// <summary>
/// Usuario del sistema. Sustituye a ASP.NET Core Identity: la tabla la
/// administro yo, y el hash de la contrasena lo calcula BCrypt desde la
/// aplicacion antes de llegar aqui.
///
/// PasswordHash vive en el modelo pero nunca en el DTO de salida. El
/// modelo representa la fila; el DTO representa lo que sale por HTTP, y
/// el hash no tiene por que salir nunca.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;

    // Solo los especialistas la tienen. El CHECK
    // CK_Usuarios_EspecialidadRol obliga a que sea NULL en los demas
    // roles y obligatoria en Especialista.
    public int? EspecialidadId { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; }
}

/// <summary>
/// Los cinco roles que acepta CK_Usuarios_Rol. Los tengo aqui y no
/// escritos en el formulario para que la pantalla los pida a la API:
/// si manana agrego un rol, lo agrego al CHECK y a esta lista, y el
/// select se actualiza solo.
///
/// El orden es el que quiero ver en el desplegable, no alfabetico.
/// </summary>
public static class RolesUsuario
{
    public const string Administrador      = "Administrador";
    public const string MedicoGeneral      = "MedicoGeneral";
    public const string Fundacion          = "Fundacion";
    public const string Especialista       = "Especialista";
    public const string TecnicoLabFarmacia = "TecnicoLabFarmacia";

    public static readonly string[] Todos =
    {
        Administrador, MedicoGeneral, Fundacion, Especialista, TecnicoLabFarmacia
    };

    /// <summary>
    /// Como se escribe cada rol en pantalla. La base guarda el valor sin
    /// espacios porque va dentro de un CHECK y despues en el token;
    /// el usuario no tiene que leer "TecnicoLabFarmacia".
    /// </summary>
    public static string Etiqueta(string rol) => rol switch
    {
        Administrador      => "Administrador",
        MedicoGeneral      => "Médico general",
        Fundacion          => "Fundación",
        Especialista       => "Especialista",
        TecnicoLabFarmacia => "Técnico de laboratorio y farmacia",
        _                  => rol
    };

    public static bool EsValido(string rol) => Todos.Contains(rol);
}
