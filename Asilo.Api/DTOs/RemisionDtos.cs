using System.ComponentModel.DataAnnotations;
using Asilo.Api.Models;

namespace Asilo.Api.DTOs;

public class RemisionCreateDto
{
    [Required(ErrorMessage = "Debe indicar el residente.")]
    [Range(1, int.MaxValue, ErrorMessage = "El ResidenteId debe ser mayor a cero.")]
    public int ResidenteId { get; set; }

    [Required(ErrorMessage = "Debe indicar el medico que remite.")]
    [Range(1, int.MaxValue, ErrorMessage = "El MedicoGeneralId debe ser mayor a cero.")]
    public int MedicoGeneralId { get; set; }

    [Required(ErrorMessage = "La sintomatologia es obligatoria.")]
    public string Sintomatologia { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe indicar la especialidad solicitada.")]
    [Range(1, int.MaxValue, ErrorMessage = "La especialidad debe venir del catalogo.")]
    public int EspecialidadId { get; set; }

    // Opcional: no toda remision lleva enfermero asignado de entrada.
    public int? EnfermeroId { get; set; }
}

// Devuelvo los nombres resueltos y no solo los Id: asi el frontend
// no tiene que hacer una peticion extra por cada fila de la tabla.
public class RemisionDto
{
    public int Id { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public int MedicoGeneralId { get; set; }
    public string MedicoNombre { get; set; } = string.Empty;
    public string Sintomatologia { get; set; } = string.Empty;
    public int EspecialidadId { get; set; }
    public string EspecialidadSolicitada { get; set; } = string.Empty;
    public int? EnfermeroId { get; set; }
    public string? EnfermeroAsignado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int DiasEspera { get; set; }
}

public class CambiarEstadoDto
{
    [Required]
    public EstadoRemision Estado { get; set; }
}
