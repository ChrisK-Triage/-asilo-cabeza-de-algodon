using System.ComponentModel.DataAnnotations;

namespace Asilo.Api.DTOs;

// Entrada: lo que el cliente envia. Sin Id, lo asigna la base de datos.
public class ResidenteCreateDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100)]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime FechaNacimiento { get; set; }

    // Trece digitos exactos: es el formato del DPI guatemalteco.
    // Opcional porque los registros que ya existen no lo tienen y
    // rellenarlo con un valor inventado seria peor que dejarlo vacio.
    [RegularExpression(@"^\d{13}$", ErrorMessage = "El DPI debe tener exactamente 13 digitos.")]
    public string? Dpi { get; set; }

    public string? Psicopatologias { get; set; }
    public string? MedicamentosCajon { get; set; }
}

// Salida: agrego Edad y TotalFamiliares, que no son columnas
// sino datos derivados que la pantalla necesita mostrar.
public class ResidenteDto
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string? Dpi { get; set; }
    public DateTime FechaNacimiento { get; set; }
    public int Edad { get; set; }
    public string? Psicopatologias { get; set; }
    public string? MedicamentosCajon { get; set; }
    public DateTime FechaIngreso { get; set; }
    public bool Activo { get; set; }
    public int TotalFamiliares { get; set; }
}
