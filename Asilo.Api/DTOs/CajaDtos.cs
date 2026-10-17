using System.ComponentModel.DataAnnotations;
using Asilo.Api.Models;

namespace Asilo.Api.DTOs;

// ---------------- Donaciones ----------------

public class DonacionCreateDto
{
    [Required(ErrorMessage = "El nombre del donante es obligatorio.")]
    [MaxLength(150)]
    public string NombreDonante { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de donante es obligatorio.")]
    public TipoDonante Tipo { get; set; }

    // Mayor a cero, no "no negativo": una donacion de cero no es una
    // donacion. Lo respalda CK_Donaciones_Monto en la base.
    [Range(0.01, 99999999, ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateTime Fecha { get; set; }

    [MaxLength(300)]
    public string? Descripcion { get; set; }
}

public class DonacionDto
{
    public int Id { get; set; }
    public string NombreDonante { get; set; } = string.Empty;
    public TipoDonante Tipo { get; set; }
    public string TipoEtiqueta { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
    public string? Descripcion { get; set; }
}

// ---------------- Gastos operativos ----------------

public class GastoCreateDto
{
    [Required(ErrorMessage = "El concepto es obligatorio.")]
    [MaxLength(150)]
    public string Concepto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoria es obligatoria.")]
    [MaxLength(50)]
    public string Categoria { get; set; } = string.Empty;

    [Range(0.01, 99999999, ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateTime Fecha { get; set; }
}

public class GastoDto
{
    public int Id { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string CategoriaEtiqueta { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; }
}

/// <summary>Una categoria de gasto, como la pide el desplegable.</summary>
public class CategoriaGastoDto
{
    public string Valor { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
}
