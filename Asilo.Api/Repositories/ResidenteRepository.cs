using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

// Todo el SQL del sistema vive en los repositorios.
// Ni los controladores ni los servicios arman consultas.
public class ResidenteRepository : IResidenteRepository
{
    private readonly DapperContext _context;

    public ResidenteRepository(DapperContext context) => _context = context;

    public async Task<IEnumerable<ResidenteDto>> ObtenerTodosAsync()
    {
        // DATEDIFF con la correccion por cumpleanos: si todavia no cumple
        // este ano, resto 1. Calcularlo en SQL evita traer la fecha y
        // procesarla en C# fila por fila.
        const string sql = @"
            SELECT  r.Id,
                    r.Nombres,
                    r.Apellidos,
                r.Dpi,
                r.CuotaMensual,
                    r.FechaNacimiento,
                    DATEDIFF(YEAR, r.FechaNacimiento, GETDATE())
                      - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, r.FechaNacimiento, GETDATE()),
                                          r.FechaNacimiento) > GETDATE()
                             THEN 1 ELSE 0 END          AS Edad,
                    r.Psicopatologias,
                    r.MedicamentosCajon,
                    r.FechaIngreso,
                    r.Activo,
                    (SELECT COUNT(*) FROM Familiares f
                     WHERE f.ResidenteId = r.Id)         AS TotalFamiliares
            FROM Residentes r
            WHERE r.Activo = 1
            ORDER BY r.Apellidos, r.Nombres;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<ResidenteDto>(sql);
    }

    public async Task<ResidenteDto?> ObtenerPorIdAsync(int id)
    {
        const string sql = @"
            SELECT  r.Id,
                    r.Nombres,
                    r.Apellidos,
                    r.Dpi,
                    r.CuotaMensual,
                    r.FechaNacimiento,
                    DATEDIFF(YEAR, r.FechaNacimiento, GETDATE())
                      - CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, r.FechaNacimiento, GETDATE()),
                                          r.FechaNacimiento) > GETDATE()
                             THEN 1 ELSE 0 END          AS Edad,
                    r.Psicopatologias,
                    r.MedicamentosCajon,
                    r.FechaIngreso,
                    r.Activo,
                    (SELECT COUNT(*) FROM Familiares f
                     WHERE f.ResidenteId = r.Id)         AS TotalFamiliares
            FROM Residentes r
            WHERE r.Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleOrDefaultAsync<ResidenteDto>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(Residente residente)
    {
        // SCOPE_IDENTITY devuelve el Id que SQL Server acaba de generar.
        const string sql = @"
            INSERT INTO Residentes (Nombres, Apellidos, Dpi, FechaNacimiento,
                                    CuotaMensual, Psicopatologias, MedicamentosCajon, Activo)
            VALUES (@Nombres, @Apellidos, @Dpi, @FechaNacimiento,
                    @CuotaMensual, @Psicopatologias, @MedicamentosCajon, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, residente);
    }

    public async Task<bool> ActualizarAsync(Residente residente)
    {
        const string sql = @"
            UPDATE Residentes
            SET Nombres           = @Nombres,
                Apellidos         = @Apellidos,
                Dpi               = @Dpi,
                CuotaMensual      = @CuotaMensual,
                FechaNacimiento   = @FechaNacimiento,
                Psicopatologias   = @Psicopatologias,
                MedicamentosCajon = @MedicamentosCajon
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, residente) > 0;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        // Baja logica. Un expediente clinico no se borra: perder historial
        // medico es un problema legal, no solo tecnico.
        const string sql = "UPDATE Residentes SET Activo = 0 WHERE Id = @Id AND Activo = 1;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    public async Task<bool> ExisteAsync(int id)
    {
        const string sql = "SELECT COUNT(1) FROM Residentes WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteScalarAsync<int>(sql, new { Id = id }) > 0;
    }
}
