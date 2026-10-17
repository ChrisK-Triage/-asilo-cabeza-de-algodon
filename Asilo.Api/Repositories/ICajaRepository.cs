using Asilo.Api.DTOs;
using Asilo.Api.Models;

namespace Asilo.Api.Repositories;

/// <summary>
/// Donaciones y gastos operativos (RF-07).
///
/// Van en un repositorio comun porque son las dos caras del libro: lo
/// que entra al asilo por fuera de las cuotas y lo que sale. El reporte
/// de ingresos contra egresos los necesita a los dos juntos, y tenerlos
/// separados obligaria a cruzarlos en C# lo que SQL hace en una
/// consulta.
/// </summary>
public interface ICajaRepository
{
    Task<IEnumerable<DonacionDto>> ObtenerDonacionesAsync(DateTime? desde, DateTime? hasta);
    Task<DonacionDto?> ObtenerDonacionPorIdAsync(int id);
    Task<int> CrearDonacionAsync(Donacion donacion);
    Task<bool> EliminarDonacionAsync(int id);

    Task<IEnumerable<GastoDto>> ObtenerGastosAsync(DateTime? desde, DateTime? hasta, string? categoria);
    Task<GastoDto?> ObtenerGastoPorIdAsync(int id);
    Task<int> CrearGastoAsync(GastoOperativo gasto);
    Task<bool> EliminarGastoAsync(int id);
}
