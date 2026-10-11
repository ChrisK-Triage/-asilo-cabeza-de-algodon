using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class FamiliarRepository : IFamiliarRepository
{
    private readonly DapperContext _context;

    public FamiliarRepository(DapperContext context) => _context = context;

    private const string SqlBase = @"
        SELECT  f.Id,
                f.ResidenteId,
                r.Nombres + ' ' + r.Apellidos AS ResidenteNombre,
                f.Nombre,
                f.Email,
                f.Telefono
        FROM Familiares f
        INNER JOIN Residentes r ON r.Id = f.ResidenteId";

    public async Task<IEnumerable<FamiliarDto>> ObtenerPorResidenteAsync(int residenteId)
    {
        string sql = SqlBase + " WHERE f.ResidenteId = @ResidenteId ORDER BY f.Nombre;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<FamiliarDto>(sql, new { ResidenteId = residenteId });
    }

    public async Task<FamiliarDto?> ObtenerPorIdAsync(int id)
    {
        string sql = SqlBase + " WHERE f.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<FamiliarDto>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(Familiar familiar)
    {
        const string sql = @"
            INSERT INTO Familiares (ResidenteId, Nombre, Email, Telefono)
            VALUES (@ResidenteId, @Nombre, @Email, @Telefono);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, familiar);
    }

    public async Task<bool> ActualizarAsync(int id, FamiliarUpdateDto dto)
    {
        const string sql = @"
            UPDATE Familiares
            SET Nombre   = @Nombre,
                Email    = @Email,
                Telefono = @Telefono
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id, dto.Nombre, dto.Email, dto.Telefono }) > 0;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        // Aqui si borro fisicamente: a diferencia del expediente clinico,
        // un contacto equivocado no es informacion que convenga conservar.
        const string sql = "DELETE FROM Familiares WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    /// <summary>
    /// Evito dos contactos con el mismo correo para un residente: el
    /// microservicio de notificaciones enviaria el aviso dos veces.
    /// </summary>
    public async Task<bool> EmailRepetidoAsync(int residenteId, string email, int? excluirId = null)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Familiares
            WHERE ResidenteId = @ResidenteId
              AND Email = @Email
              AND (@ExcluirId IS NULL OR Id <> @ExcluirId);";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new
        {
            ResidenteId = residenteId,
            Email = email,
            ExcluirId = excluirId
        }) > 0;
    }
}
