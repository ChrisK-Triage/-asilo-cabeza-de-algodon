using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly DapperContext _context;

    public UsuarioRepository(DapperContext context) => _context = context;

    // El SELECT no menciona PasswordHash. En su lugar manda un booleano
    // calculado sobre el prefijo: BCrypt escribe sus hashes empezando en
    // '$2a$', '$2b$' o '$2y$', asi que cualquier cosa que no arranque
    // con '$2' es el marcador 'PENDIENTE_HASH_BCRYPT' que dejo el script
    // de semilla. Resolverlo asi me evita agregar una columna solo para
    // saber algo que el dato ya dice.
    //
    // RegistrosAsociados suma las dos formas en que un usuario queda
    // amarrado al historial clinico: como medico que emitio la remision
    // y como especialista que atendio la cita.
    private const string SqlBase = @"
        SELECT  u.Id,
                u.NombreCompleto,
                u.Email,
                u.Rol,
                u.EspecialidadId,
                e.Nombre                                    AS Especialidad,
                u.Activo,
                u.FechaRegistro,
                CASE WHEN u.PasswordHash LIKE '$2%'
                     THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
                                                            AS TieneContrasenaDefinida,
                (SELECT COUNT(*) FROM Remisiones r WHERE r.MedicoGeneralId = u.Id)
              + (SELECT COUNT(*) FROM CitasFundacion c WHERE c.EspecialistaId = u.Id)
                                                            AS RegistrosAsociados
        FROM Usuarios u
        LEFT JOIN Especialidades e ON e.Id = u.EspecialidadId";

    public async Task<IEnumerable<UsuarioDto>> ObtenerTodosAsync(bool soloActivos = true)
    {
        // Los activos primero y despues por nombre: en un listado con
        // bajas, las bajas no deben disputarle el lugar a quien si
        // trabaja aqui.
        string sql = SqlBase
                   + (soloActivos ? " WHERE u.Activo = 1" : "")
                   + " ORDER BY u.Activo DESC, u.Id DESC;";

        using var conexion = _context.CreateConnection();
        var usuarios = await conexion.QueryAsync<UsuarioDto>(sql);

        return usuarios.Select(Etiquetar);
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(int id)
    {
        string sql = SqlBase + " WHERE u.Id = @Id;";

        using var conexion = _context.CreateConnection();
        var usuario = await conexion.QuerySingleOrDefaultAsync<UsuarioDto>(sql, new { Id = id });

        return usuario is null ? null : Etiquetar(usuario);
    }

    // La etiqueta la pongo aqui y no en el SELECT con un CASE porque ya
    // vive en RolesUsuario. Tenerla en los dos lados obligaria a
    // recordar actualizar el SQL cada vez que cambie un nombre.
    private static UsuarioDto Etiquetar(UsuarioDto dto)
    {
        dto.RolEtiqueta = RolesUsuario.Etiqueta(dto.Rol);
        return dto;
    }

    public async Task<Usuario?> ObtenerPorEmailParaLoginAsync(string email)
    {
        // Aqui si traigo el hash: es lo que BCrypt.Verify necesita
        // comparar. Solo lo llama el inicio de sesion.
        const string sql = @"
            SELECT Id, NombreCompleto, Email, PasswordHash, Rol,
                   EspecialidadId, Activo, FechaRegistro
            FROM Usuarios
            WHERE Email = @Email AND Activo = 1;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { Email = email });
    }

    public async Task<int> CrearAsync(Usuario usuario)
    {
        const string sql = @"
            INSERT INTO Usuarios (NombreCompleto, Email, PasswordHash, Rol,
                                  EspecialidadId, Activo)
            VALUES (@NombreCompleto, @Email, @PasswordHash, @Rol,
                    @EspecialidadId, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, usuario);
    }

    public async Task<bool> ActualizarAsync(Usuario usuario)
    {
        // El UPDATE no toca PasswordHash. Si lo incluyera, cada guardado
        // del formulario tendria que acordarse de mandar el hash actual,
        // y olvidarlo dejaria al usuario sin poder entrar.
        const string sql = @"
            UPDATE Usuarios
            SET NombreCompleto = @NombreCompleto,
                Email          = @Email,
                Rol            = @Rol,
                EspecialidadId = @EspecialidadId
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, usuario) > 0;
    }

    public async Task<bool> CambiarPasswordAsync(int id, string passwordHash)
    {
        const string sql = "UPDATE Usuarios SET PasswordHash = @PasswordHash WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id, PasswordHash = passwordHash }) > 0;
    }

    /// <summary>
    /// Baja logica, igual que en residentes. Un usuario con remisiones o
    /// citas a su nombre no se puede borrar: la llave foranea lo impide
    /// y, si se pudiera, el historial clinico quedaria sin autor.
    /// </summary>
    public async Task<bool> DesactivarAsync(int id)
    {
        const string sql = "UPDATE Usuarios SET Activo = 0 WHERE Id = @Id AND Activo = 1;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ReactivarAsync(int id)
    {
        const string sql = "UPDATE Usuarios SET Activo = 1 WHERE Id = @Id AND Activo = 0;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ExisteAsync(int id)
    {
        const string sql = "SELECT COUNT(1) FROM Usuarios WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Id = id }) > 0;
    }

    /// <summary>
    /// El correo es la credencial de acceso, asi que no puede repetirse.
    /// La base ya lo impide con UQ_Usuarios_Email, pero chocar contra el
    /// indice unico produce un error 500 con texto de SQL Server; esto
    /// permite responder 409 con un mensaje que el usuario entienda.
    ///
    /// Incluye a los usuarios dados de baja: su correo sigue ocupado.
    /// </summary>
    public async Task<bool> EmailRepetidoAsync(string email, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Usuarios
            WHERE Email = @Email
              AND (@ExcluirId IS NULL OR Id <> @ExcluirId);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(
            sql, new { Email = email, ExcluirId = excluirId }) > 0;
    }

    public async Task<bool> HayOtroAdministradorActivoAsync(int excluirId)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Usuarios
            WHERE Rol = @Rol AND Activo = 1 AND Id <> @ExcluirId;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(
            sql, new { Rol = RolesUsuario.Administrador, ExcluirId = excluirId }) > 0;
    }

    public async Task<int> ContarCitasPendientesComoEspecialistaAsync(int id)
    {
        // Estado 0=Programada, 1=EnEvaluacion, 2=EsperandoAnexos son las
        // que todavia esperan algo de el. Las de estado 3 y 4 ya estan
        // cerradas y son historial: esas no estorban.
        const string sql = @"
            SELECT COUNT(1)
            FROM CitasFundacion
            WHERE EspecialistaId = @Id AND Estado IN (0, 1, 2);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Id = id });
    }
}
