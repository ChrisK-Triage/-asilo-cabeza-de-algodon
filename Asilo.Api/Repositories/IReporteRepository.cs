using Asilo.Api.DTOs;

namespace Asilo.Api.Repositories;

/// <summary>
/// Los cinco reportes que pide RF-09.
///
/// Cada uno es una consulta de agregacion que cruza lo clinico con lo
/// contable. Es la razon por la que el proyecto usa Dapper: estas
/// consultas se escriben directo en SQL, y expresarlas en LINQ habria
/// sido traducir a la fuerza lo que el motor hace mejor.
/// </summary>
public interface IReporteRepository
{
    Task<ReporteHistorialDto?> HistorialClinicoAsync(int residenteId, DateTime? desde, DateTime? hasta);
    Task<ReporteCostosCitaDto> CostosPorCitaAsync(DateTime? desde, DateTime? hasta);
    Task<ReporteIngresosEgresosDto> IngresosEgresosAsync(DateTime? desde, DateTime? hasta);
    Task<ReportePagosFundacionDto> PagosALaFundacionAsync(DateTime? desde, DateTime? hasta);
    Task<ReporteInsumosDto> ExamenesEInsumosAsync(int? residenteId, DateTime? desde, DateTime? hasta);
}
