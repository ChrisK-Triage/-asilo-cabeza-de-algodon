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

    /// <summary>
    /// Combinaciones que usan los atributos [Authorize(Roles = ...)].
    /// Tienen que ser const porque un atributo se resuelve en tiempo de
    /// compilacion; por eso son concatenaciones y no arreglos.
    ///
    /// Les pongo nombre en vez de escribir la lista en cada controlador
    /// para que el permiso se lea como una intencion y no como una
    /// enumeracion que hay que descifrar.
    /// </summary>
    public static class Permiso
    {
        public const string SoloAdministrador = Administrador;

        /// <summary>Quien atiende el expediente del residente.</summary>
        public const string Clinico = Administrador + "," + MedicoGeneral;

        /// <summary>
        /// Admision: dar de alta y de baja al residente, su DPI, su fecha
        /// de ingreso, su cuota de estancia y sus contactos familiares.
        ///
        /// No es trabajo clinico sino administrativo: lo que se firma con
        /// la familia al recibir al residente. El medico general puede
        /// leerlo, pero quien lo captura es administracion.
        /// </summary>
        public const string Admision = Administrador;

        /// <summary>
        /// Los datos clinicos del expediente: psicopatologias y
        /// medicamentos de cajon. Es lo unico del residente que el
        /// medico general si escribe.
        /// </summary>
        public const string ExpedienteClinico = Administrador + "," + MedicoGeneral;

        /// <summary>Quien puede leer el expediente sin modificarlo.</summary>
        public const string LecturaClinica =
            Administrador + "," + MedicoGeneral + "," + Fundacion + "," + Especialista;

        /// <summary>Quien coordina la agenda de la fundacion.</summary>
        public const string Coordinacion = Administrador + "," + Fundacion;

        /// <summary>
        /// Quien puede leer la agenda de citas. Son los cinco roles, pero
        /// cada uno por una razon distinta: la fundacion la arma, el
        /// especialista atiende la suya, el medico sigue sus remisiones y
        /// el tecnico necesita saber a que cita pertenece el examen o el
        /// despacho que esta registrando.
        ///
        /// Lo escribo enumerado y no como [Authorize] a secas para que un
        /// rol nuevo no herede el acceso sin que alguien lo decida.
        /// </summary>
        public const string AgendaCitas =
            Administrador + "," + MedicoGeneral + "," + Fundacion + ","
          + Especialista + "," + TecnicoLabFarmacia;

        /// <summary>Quien emite y consulta remisiones.</summary>
        public const string Remisiones =
            Administrador + "," + MedicoGeneral + "," + Fundacion;

        /// <summary>Quien atiende la consulta y asienta el diagnostico.</summary>
        public const string Consulta = Administrador + "," + Especialista;

        /// <summary>
        /// Examenes y medicamentos. Incluye al especialista porque es
        /// quien los solicita durante la consulta; el tecnico es quien
        /// despues carga el resultado y despacha.
        /// </summary>
        public const string Anexos =
            Administrador + "," + TecnicoLabFarmacia + "," + Especialista;

        /// <summary>
        /// Quien ejecuta lo que el especialista indico: carga el
        /// resultado del examen y entrega el medicamento, poniendo el
        /// precio en los dos casos.
        ///
        /// Es el par de Consulta. El especialista pide, el tecnico hace;
        /// ninguno de los dos invade al otro.
        /// </summary>
        public const string Procesamiento = Administrador + "," + TecnicoLabFarmacia;

        /// <summary>
        /// Roles que no deben ver psicopatologias ni medicamentos de
        /// cajon: coordinan o procesan, pero no tratan al residente.
        /// </summary>
        public static readonly string[] SinAccesoClinico = { Fundacion, TecnicoLabFarmacia };
    }
}
