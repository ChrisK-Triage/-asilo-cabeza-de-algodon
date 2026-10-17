using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class CajaRepository : ICajaRepository
{
    private readonly DapperContext _context;

    public CajaRepository(DapperContext context) => _context = context;

    // El rango de fechas se arma igual en los dos listados: si el
    // parametro viene nulo, ese extremo no filtra. Asi un solo endpoint
    // sirve para "todo", "desde una fecha" y "entre dos".
    private const string FiltroFechas = @"
        AND (@Desde IS NULL OR Fecha >= @Desde)
        AND (@Hasta IS NULL OR Fecha <= @Hasta)";

    // ==================== DONACIONES ====================

    public async Task<IEnumerable<DonacionDto>> ObtenerDonacionesAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT Id, NombreDonante, Tipo, Monto, Fecha, Descripcion
            FROM Donaciones
            WHERE 1 = 1 {FiltroFechas}
            ORDER BY Fecha DESC, Id DESC;";

        using var conexion = _context.CreateConnection();
        var donaciones = await conexion.QueryAsync<DonacionDto>(sql, new { Desde = desde, Hasta = hasta });

        return donaciones.Select(Etiquetar);
    }

    public async Task<DonacionDto?> ObtenerDonacionPorIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, NombreDonante, Tipo, Monto, Fecha, Descripcion
            FROM Donaciones WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        var donacion = await conexion.QuerySingleOrDefaultAsync<DonacionDto>(sql, new { Id = id });

        return donacion is null ? null : Etiquetar(donacion);
    }

    public async Task<int> CrearDonacionAsync(Donacion donacion)
    {
        const string sql = @"
            INSERT INTO Donaciones (NombreDonante, Tipo, Monto, Fecha, Descripcion)
            VALUES (@NombreDonante, @Tipo, @Monto, @Fecha, @Descripcion);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, donacion);
    }

    /// <summary>
    /// Borrado real y no baja logica, al contrario que residentes o
    /// usuarios. Una donacion no tiene historial que preservar ni nada
    /// que la referencie: si se capturo mal, lo correcto es quitarla,
    /// no dejarla marcada. El trigger de auditoria guarda el rastro.
    /// </summary>
    public async Task<bool> EliminarDonacionAsync(int id)
    {
        const string sql = "DELETE FROM Donaciones WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    private static DonacionDto Etiquetar(DonacionDto dto)
    {
        dto.TipoEtiqueta = TiposDonante.Etiqueta(dto.Tipo);
        return dto;
    }

    // ==================== GASTOS OPERATIVOS ====================

    public async Task<IEnumerable<GastoDto>> ObtenerGastosAsync(
        DateTime? desde, DateTime? hasta, string? categoria)
    {
        string sql = $@"
            SELECT Id, Concepto, Categoria, Monto, Fecha
            FROM GastosOperativos
            WHERE 1 = 1 {FiltroFechas}
              AND (@Categoria IS NULL OR Categoria = @Categoria)
            ORDER BY Fecha DESC, Id DESC;";

        using var conexion = _context.CreateConnection();
        var gastos = await conexion.QueryAsync<GastoDto>(
            sql, new { Desde = desde, Hasta = hasta, Categoria = categoria });

        return gastos.Select(Etiquetar);
    }

    public async Task<GastoDto?> ObtenerGastoPorIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, Concepto, Categoria, Monto, Fecha
            FROM GastosOperativos WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        var gasto = await conexion.QuerySingleOrDefaultAsync<GastoDto>(sql, new { Id = id });

        return gasto is null ? null : Etiquetar(gasto);
    }

    public async Task<int> CrearGastoAsync(GastoOperativo gasto)
    {
        const string sql = @"
            INSERT INTO GastosOperativos (Concepto, Categoria, Monto, Fecha)
            VALUES (@Concepto, @Categoria, @Monto, @Fecha);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<int>(sql, gasto);
    }

    public async Task<bool> EliminarGastoAsync(int id)
    {
        const string sql = "DELETE FROM GastosOperativos WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.ExecuteAsync(sql, new { Id = id }) > 0;
    }

    private static GastoDto Etiquetar(GastoDto dto)
    {
        dto.CategoriaEtiqueta = CategoriasGasto.Etiqueta(dto.Categoria);
        return dto;
    }
}
