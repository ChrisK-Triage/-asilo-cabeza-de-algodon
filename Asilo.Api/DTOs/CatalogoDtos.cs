using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

// ---------------- Especialidades ----------------

/// <summary>
/// Una especialidad y cuantos especialistas activos la atienden.
///
/// El conteo es el dato que faltaba: el formulario de remision tiene
/// que poder ofrecer todas las especialidades pero advertir cuales no
/// tienen cobertura. Una remision a una especialidad descubierta sigue
/// siendo informacion clinica valida, solo que nadie la puede atender
/// todavia, y el medico merece saberlo antes de emitirla.
/// </summary>
public class EspecialidadDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public int EspecialistasActivos { get; set; }
    public bool TieneCobertura => EspecialistasActivos > 0;
}

public class EspecialidadCreateDto
{
    [Required(ErrorMessage = "El nombre de la especialidad es obligatorio.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;
}

// ---------------- Enfermeros ----------------

public class EnfermeroDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
}

public class EnfermeroCreateDto
{
    [Required(ErrorMessage = "El nombre del enfermero es obligatorio.")]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefono { get; set; }
}
