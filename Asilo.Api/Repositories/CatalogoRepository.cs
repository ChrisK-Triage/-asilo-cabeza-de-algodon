using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class CatalogoRepository : ICatalogoRepository
{
    private readonly DapperContext _context;

    public CatalogoRepository(DapperContext context) => _context = context;

    // ==================== ESPECIALIDADES ====================

    // El LEFT JOIN cuenta los especialistas que la atienden. Va en la
    // misma consulta para que la pantalla no tenga que pedir el
    // catalogo y despues preguntar uno por uno quien lo cubre.
    private const string SqlEspecialidades = @"
        SELECT  e.Id,
                e.Nombre,
                e.Activa,
                COUNT(u.Id) AS EspecialistasActivos
        FROM Especialidades e
        LEFT JOIN Usuarios u
               ON u.EspecialidadId = e.Id
              AND u.Rol = 'Especialista'
              AND u.Activo = 1";

    public async Task<IEnumerable<EspecialidadDto>> ObtenerEspecialidadesAsync(bool soloActivas = true)
    {
        string sql = SqlEspecialidades
                   + (soloActivas ? " WHERE e.Activa = 1" : "")
                   + " GROUP BY e.Id, e.Nombre, e.Activa ORDER BY e.Nombre;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<EspecialidadDto>(sql);
    }

    public async Task<EspecialidadDto?> ObtenerEspecialidadPorIdAsync(int id)
    {
        string sql = SqlEspecialidades
                   + " WHERE e.Id = @Id GROUP BY e.Id, e.Nombre, e.Activa;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<EspecialidadDto>(sql, new { Id = id });
    }

    public async Task<int> CrearEspecialidadAsync(Especialidad especialidad)
    {
        const string sql = @"
            INSERT INTO Especialidades (Nombre, Activa)
            VALUES (@Nombre, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, especialidad);
    }

    /// <summary>
    /// Baja logica. No borro: hay remisiones apuntando a la
    /// especialidad y su historial tiene que seguir leyendose.
    /// </summary>
    public async Task<bool> DesactivarEspecialidadAsync(int id)
    {
        const string sql = "UPDATE Especialidades SET Activa = 0 WHERE Id = @Id AND Activa = 1;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    /// <summary>
    /// Comparo sin tildes ni mayusculas: "Neurologia" y "Neurología"
    /// son la misma especialidad y tener las dos reintroduciria el
    /// problema que este catalogo vino a resolver.
    /// </summary>
    public async Task<bool> EspecialidadRepetidaAsync(string nombre, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Especialidades
            WHERE Nombre = @Nombre COLLATE Modern_Spanish_CI_AI
              AND (@ExcluirId IS NULL OR Id <> @ExcluirId);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Nombre = nombre, ExcluirId = excluirId }) > 0;
    }

    // ==================== ENFERMEROS ====================

    public async Task<IEnumerable<EnfermeroDto>> ObtenerEnfermerosAsync(bool soloActivos = true)
    {
        string sql = "SELECT Id, Nombre, Telefono, Activo FROM Enfermeros"
                   + (soloActivos ? " WHERE Activo = 1" : "")
                   + " ORDER BY Nombre;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<EnfermeroDto>(sql);
    }

    public async Task<EnfermeroDto?> ObtenerEnfermeroPorIdAsync(int id)
    {
        const string sql = "SELECT Id, Nombre, Telefono, Activo FROM Enfermeros WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<EnfermeroDto>(sql, new { Id = id });
    }

    public async Task<int> CrearEnfermeroAsync(Enfermero enfermero)
    {
        const string sql = @"
            INSERT INTO Enfermeros (Nombre, Telefono, Activo)
            VALUES (@Nombre, @Telefono, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, enfermero);
    }

    public async Task<bool> DesactivarEnfermeroAsync(int id)
    {
        const string sql = "UPDATE Enfermeros SET Activo = 0 WHERE Id = @Id AND Activo = 1;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> EnfermeroRepetidoAsync(string nombre, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Enfermeros
            WHERE Nombre = @Nombre COLLATE Modern_Spanish_CI_AI
              AND (@ExcluirId IS NULL OR Id <> @ExcluirId);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Nombre = nombre, ExcluirId = excluirId }) > 0;
    }
}
