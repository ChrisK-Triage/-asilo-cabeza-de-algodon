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

    // Dos banderas y no una: el especialista puede pedir solo examenes,
    // solo medicamento, los dos o ninguno. Con un unico booleano no se
    // podia expresar la diferencia, y sobre todo no quedaba registro de
    // QUE se pidio, que es lo que define cuando la bitacora cierra.
    //
    // Las dos en false: la cita pasa directo a BitacoraCompletada.
    // Alguna en true: queda en EsperandoAnexos hasta que se registre
    // lo solicitado.
    public bool RequiereLaboratorio { get; set; }
    public bool RequiereFarmacia { get; set; }
}

public class CitaDto
{
    public int Id { get; set; }
    public int RemisionId { get; set; }
    public int ResidenteId { get; set; }
    public string ResidenteNombre { get; set; } = string.Empty;
    public int EspecialidadId { get; set; }
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

    // Las expongo para que la pantalla pueda decir que esta esperando
    // una cita, en lugar de un "Esperando anexos" que no dice cual.
    public bool RequiereLaboratorio { get; set; }
    public bool RequiereFarmacia { get; set; }
}

// Para llenar el select de especialistas en la consola de la fundacion.
public class EspecialistaDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;

    // El Id es lo que se compara contra la remision; el nombre es solo
    // para mostrarlo. Antes se comparaban dos cadenas de texto y una
    // tilde de diferencia rompia el emparejamiento.
    public int EspecialidadId { get; set; }
    public string? Especialidad { get; set; }
}
