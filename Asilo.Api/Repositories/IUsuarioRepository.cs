using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

public interface IUsuarioRepository
{
    Task<IEnumerable<UsuarioDto>> ObtenerTodosAsync(bool soloActivos = true);
    Task<UsuarioDto?> ObtenerPorIdAsync(int id);

    /// <summary>
    /// Devuelve el modelo completo, con hash. Es el unico metodo que lo
    /// expone y existe para el inicio de sesion: ahi hace falta el hash
    /// para compararlo. Ningun controlador debe devolver lo que esto
    /// retorna sin convertirlo antes a UsuarioDto.
    /// </summary>
    Task<Usuario?> ObtenerPorEmailParaLoginAsync(string email);

    Task<int> CrearAsync(Usuario usuario);
    Task<bool> ActualizarAsync(Usuario usuario);
    Task<bool> CambiarPasswordAsync(int id, string passwordHash);
    Task<bool> DesactivarAsync(int id);
    Task<bool> ReactivarAsync(int id);

    Task<bool> ExisteAsync(int id);
    Task<bool> EmailRepetidoAsync(string email, int? excluirId = null);

    /// <summary>
    /// Si queda algun administrador activo distinto del que paso. Lo uso
    /// antes de dar de baja a un administrador o de cambiarle el rol:
    /// sin esto el sistema se puede quedar sin nadie que administre
    /// usuarios, y no hay forma de recuperarlo desde la aplicacion.
    /// </summary>
    Task<bool> HayOtroAdministradorActivoAsync(int excluirId);

    /// <summary>
    /// Citas en estado no final asignadas a un especialista. Si tiene,
    /// quitarle el rol dejaria esas citas a cargo de alguien que ya no
    /// es especialista.
    /// </summary>
    Task<int> ContarCitasPendientesComoEspecialistaAsync(int id);
}
