using Dapper;

namespace Asilo.Api.Data;

/// <summary>
/// Le asigna contrasena a los usuarios que todavia tienen el marcador
/// 'PENDIENTE_HASH_BCRYPT' del script de semilla.
///
/// Por que existe. El hash de BCrypt no se puede escribir a mano en un
/// script SQL: lo calcula la libreria, con su propia sal y su factor de
/// costo. Mientras no corriera por aqui, ninguno de los seis usuarios
/// de la semilla podia entrar, y como el panel de administracion de
/// usuarios tambien exige sesion, el sistema se quedaba sin forma de
/// darse de alta a si mismo.
///
/// Por que es seguro dejarlo. Solo toca filas cuyo hash no tiene
/// formato BCrypt, asi que una contrasena ya definida nunca se pisa, y
/// la segunda ejecucion no hace nada. Se apaga sola quitando
/// 'Seguridad:PasswordInicial' de la configuracion, que es lo que hay
/// que hacer antes de publicar.
/// </summary>
public static class SemillaSeguridad
{
    public static async Task AsignarPasswordsPendientesAsync(
        DapperContext contexto, IConfiguration configuracion, ILogger logger)
    {
        string? passwordInicial = configuracion["Seguridad:PasswordInicial"];

        if (string.IsNullOrWhiteSpace(passwordInicial))
        {
            logger.LogInformation(
                "No hay 'Seguridad:PasswordInicial' configurada: no se asignan contrasenas iniciales.");
            return;
        }

        const string sqlPendientes = @"
            SELECT Id, Email FROM Usuarios WHERE PasswordHash NOT LIKE '$2%';";

        const string sqlAsignar = @"
            UPDATE Usuarios SET PasswordHash = @Hash
            WHERE Id = @Id AND PasswordHash NOT LIKE '$2%';";

        using var conexion = contexto.CreateConnection();

        var pendientes = (await conexion.QueryAsync<CuentaPendiente>(sqlPendientes)).ToList();

        if (pendientes.Count == 0) return;

        foreach (var cuenta in pendientes)
        {
            // Un hash distinto por usuario aunque la contrasena sea la
            // misma: BCrypt genera una sal nueva en cada llamada, asi
            // que dos cuentas con igual clave no se delatan entre si.
            await conexion.ExecuteAsync(sqlAsignar, new
            {
                cuenta.Id,
                Hash = BCrypt.Net.BCrypt.HashPassword(passwordInicial)
            });
        }

        logger.LogWarning(
            "Se asigno la contrasena inicial a {Cantidad} usuario(s): {Correos}. " +
            "Hay que cambiarla desde el panel de usuarios y quitar 'Seguridad:PasswordInicial' antes de publicar.",
            pendientes.Count,
            string.Join(", ", pendientes.Select(p => p.Email)));
    }

    // Dapper necesita un tipo con propiedades para mapear; una tupla no
    // le sirve. Queda privado porque no representa nada fuera de aqui.
    private sealed class CuentaPendiente
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
