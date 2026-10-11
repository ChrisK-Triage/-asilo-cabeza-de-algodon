using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

public class FamiliarCreateDto
{
    [Required(ErrorMessage = "Debe indicar el residente.")]
    [Range(1, int.MaxValue)]
    public int ResidenteId { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    // Trece digitos exactos: es el formato del DPI guatemalteco.
    // Opcional porque los registros que ya existen no lo tienen y
    // rellenarlo con un valor inventado seria peor que dejarlo vacio.
    [RegularExpression(@"^\d{13}$", ErrorMessage = "El DPI debe tener exactamente 13 digitos.")]
    public string? Dpi { get; set; }

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo no es valido.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefono { get; set; }
}

public class FamiliarUpdateDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    // Trece digitos exactos: es el formato del DPI guatemalteco.
    // Opcional porque los registros que ya existen no lo tienen y
    // rellenarlo con un valor inventado seria peor que dejarlo vacio.
    [RegularExpression(@"^\d{13}$", ErrorMessage = "El DPI debe tener exactamente 13 digitos.")]
    public string? Dpi { get; set; }

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo no es valido.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefono { get; set; }
}

public class FamiliarDto
{
    public int Id { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Dpi { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}
