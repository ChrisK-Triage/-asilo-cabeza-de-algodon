using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Services;

public interface IServicioToken
{
    /// <summary>
    /// Arma el token firmado de un usuario ya autenticado y la sesion
    /// que acompana la respuesta.
    /// </summary>
    SesionDto CrearSesion(Usuario usuario, string? especialidad);
}
