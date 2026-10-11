using System.ComponentModel.DataAnnotations;
using Asilo.Api.Models;

namespace Asilo.Api.DTOs;

// La fundacion asigna especialista y horario a una remision pendiente.
public class AsignarCitaDto
{
    [Required(ErrorMessage = "Debe indicar la remision.")]
    [Range(1, int.MaxValue)]
    public int RemisionId { get; set; }

    [Required(ErrorMessage = "Debe indicar el especialista.")]
    [Range(1, int.MaxValue)]
    public int EspecialistaId { get; set; }

    [Required(ErrorMessage = "Debe indicar fecha y hora de la cita.")]
    public DateTime BloqueHorario { get; set; }

    [Range(0, 999999, ErrorMessage = "El costo no puede ser negativo.")]
    public decimal CostoConsulta { get; set; }
}

// El especialista registra el resultado de la consulta.
public class DiagnosticoDto
{
    [Required(ErrorMessage = "El diagnostico es obligatorio.")]
    public string Diagnostico { get; set; } = string.Empty;

    // Si requiere laboratorio o farmacia, la cita queda en
    // EsperandoAnexos en lugar de BitacoraCompletada.
    public bool RequiereAnexos { get; set; }
}

public class CitaDto
{
    public int Id { get; set; }
    public int RemisionId { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public string EspecialidadSolicitada { get; set; } = string.Empty;
    public string Sintomatologia { get; set; } = string.Empty;
    public int EspecialistaId { get; set; }
    public string EspecialistaNombre { get; set; } = string.Empty;
    public string? EspecialistaEspecialidad { get; set; }
    public DateTime BloqueHorario { get; set; }
    public string? Diagnostico { get; set; }
    public decimal CostoConsulta { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Liquidada { get; set; }
}

// Para llenar el select de especialistas en la consola de la fundacion.
public class EspecialistaDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Especialidad { get; set; }
}
