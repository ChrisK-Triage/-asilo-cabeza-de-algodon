namespace Asilo.Api.Models;

/// <summary>
/// Especialidad medica. Antes era texto libre repetido en Usuarios y
/// en Remisiones; al ser una tabla, el nombre se escribe una sola vez
/// y las dos referencias apuntan al mismo Id.
/// </summary>
public class Especialidad
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
}

/// <summary>
/// Enfermero del asilo, responsable del traslado del residente a la
/// consulta. Antes era un VARCHAR suelto en Remisiones, a diferencia
/// del medico y del especialista, que si eran llaves foraneas.
/// </summary>
public class Enfermero
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
}
