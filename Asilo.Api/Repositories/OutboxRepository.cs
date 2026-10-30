using System.Data;
using System.Text.Json;
using Asilo.Api.Data;
using Asilo.Api.DTOs;
using Asilo.Api.Models;
using Dapper;

namespace Asilo.Api.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly DapperContext _context;

    // Sin escapar los acentos: el payload se lee a ojo en la pantalla de
    // la bandeja, y "Neurología" no se entiende de un vistazo.
    private static readonly JsonSerializerOptions Formato = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public OutboxRepository(DapperContext context) => _context = context;

    public async Task EncolarAsync(IDbConnection conexion, IDbTransaction transaccion,
                                   string tipoEvento, object contenido)
    {
        const string sql = @"
            INSERT INTO OutboxMensajes (TipoEvento, Payload, Procesado, Intentos)
            VALUES (@TipoEvento, @Payload, 0, 0);";

        await conexion.ExecuteAsync(sql, new
        {
            TipoEvento = tipoEvento,
            Payload = JsonSerializer.Serialize(contenido, Formato)
        }, transaccion);
    }

    public async Task<List<DestinatarioAviso>> ObtenerDestinatariosAsync(
        IDbConnection conexion, IDbTransaction transaccion, int residenteId)
    {
        // Solo los que tienen correo. El aviso sale por correo, asi que
        // un familiar sin direccion no es destinatario de este canal
        // aunque si lo sea del residente.
        //
        // Sin este filtro, un familiar registrado solo con telefono
        // entraba a la lista con el correo en nulo: el canal veia un
        // destinatario, no daba el aviso de "no hay a quien escribirle"
        // y reventaba despues al armar la direccion, con un error que
        // no dice cual es el problema real.
        const string sql = @"
            SELECT Nombre, Email, Telefono
            FROM Familiares
            WHERE ResidenteId = @ResidenteId
              AND Email IS NOT NULL
              AND LTRIM(RTRIM(Email)) <> ''
            ORDER BY Id;";

        var filas = await conexion.QueryAsync<DestinatarioAviso>(
            sql, new { ResidenteId = residenteId }, transaccion);

        return filas.ToList();
    }

    // ==================== DESPACHADOR ====================

    /// <summary>
    /// Los pendientes mas viejos primero, descartando los que ya
    /// agotaron los reintentos.
    ///
    /// UPDLOCK y READPAST: si hubiera dos despachadores corriendo, el
    /// segundo se salta las filas que el primero ya tomo en lugar de
    /// quedarse esperando a que las suelte. Hoy solo hay uno, pero la
    /// alternativa era un SELECT que permite que los dos tomen el mismo
    /// mensaje y el familiar reciba el aviso dos veces.
    /// </summary>
    public async Task<IEnumerable<OutboxMensaje>> TomarPendientesAsync(int maximo, int topeIntentos)
    {
        const string sql = @"
            SELECT TOP (@Maximo)
                   Id, TipoEvento, Payload, Procesado, Intentos,
                   UltimoError, FechaCreacion, FechaProcesado
            FROM OutboxMensajes WITH (UPDLOCK, READPAST)
            WHERE Procesado = 0
              AND Intentos < @TopeIntentos
            ORDER BY FechaCreacion, Id;";

        using var conexion = _context.CreateConnection();
        return await conexion.QueryAsync<OutboxMensaje>(
            sql, new { Maximo = maximo, TopeIntentos = topeIntentos });
    }

    public async Task MarcarEntregadoAsync(long id)
    {
        const string sql = @"
            UPDATE OutboxMensajes
            SET Procesado      = 1,
                Intentos       = Intentos + 1,
                FechaProcesado = GETDATE(),
                UltimoError    = NULL
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        await conexion.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Suma un intento y deja escrito que fallo. El mensaje sigue
    /// pendiente: el despachador lo volvera a tomar en la siguiente
    /// vuelta, hasta el tope.
    /// </summary>
    public async Task RegistrarFalloAsync(long id, string error)
    {
        const string sql = @"
            UPDATE OutboxMensajes
            SET Intentos    = Intentos + 1,
                UltimoError = LEFT(@Error, 1000)
            WHERE Id = @Id;";

        using var conexion = _context.CreateConnection();
        await conexion.ExecuteAsync(sql, new { Id = id, Error = error });
    }

    // ==================== CONSULTA ====================

    // Fallido no es una columna: es un pendiente que ya agoto los
    // reintentos. Lo derivo aqui para no guardar un estado que habria
    // que mantener sincronizado con el contador.
    private const string SqlSituacion = @"
        CASE WHEN Procesado = 1            THEN 'Entregado'
             WHEN Intentos >= @TopeIntentos THEN 'Fallido'
             ELSE 'Pendiente' END";

    public async Task<IEnumerable<OutboxMensajeDto>> ObtenerAsync(string? situacion, int topeIntentos)
    {
        string sql = $@"
            SELECT  Id,
                    TipoEvento,
                    Payload,
                    {SqlSituacion} AS Situacion,
                    Intentos,
                    UltimoError,
                    FechaCreacion,
                    FechaProcesado
            FROM OutboxMensajes
            {(situacion is null ? "" : $"WHERE {SqlSituacion} = @Situacion")}
            ORDER BY FechaCreacion DESC, Id DESC;";

        using var conexion = _context.CreateConnection();
        var mensajes = await conexion.QueryAsync<OutboxMensajeDto>(
            sql, new { TopeIntentos = topeIntentos, Situacion = situacion });

        return mensajes.Select(Describir);
    }

    public async Task<ResumenOutboxDto> ObtenerResumenAsync(int topeIntentos)
    {
        const string sql = @"
            SELECT
                SUM(CASE WHEN Procesado = 0 AND Intentos <  @TopeIntentos THEN 1 ELSE 0 END) AS Pendientes,
                SUM(CASE WHEN Procesado = 1                               THEN 1 ELSE 0 END) AS Entregados,
                SUM(CASE WHEN Procesado = 0 AND Intentos >= @TopeIntentos THEN 1 ELSE 0 END) AS Fallidos
            FROM OutboxMensajes;";

        using var conexion = _context.CreateConnection();
        return await conexion.QuerySingleAsync<ResumenOutboxDto>(sql, new { TopeIntentos = topeIntentos });
    }

    /// <summary>
    /// Saca del payload la etiqueta del evento y a quien iba dirigido,
    /// para que la pantalla no tenga que abrir el JSON.
    /// </summary>
    private static OutboxMensajeDto Describir(OutboxMensajeDto dto)
    {
        dto.TipoEtiqueta = TipoEventoOutbox.Etiqueta(dto.TipoEvento);

        try
        {
            using var doc = JsonDocument.Parse(dto.Payload);

            if (doc.RootElement.TryGetProperty("destinatarios", out var lista)
                && lista.ValueKind == JsonValueKind.Array)
            {
                var correos = lista.EnumerateArray()
                    .Select(d => d.TryGetProperty("email", out var e) ? e.GetString() : null)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                dto.Destinatarios = correos.Count > 0
                    ? string.Join(", ", correos)
                    : "Sin contacto registrado";
            }
        }
        catch
        {
            // Un payload que no se puede leer no debe tumbar la pantalla:
            // justamente es lo que hay que poder ver para arreglarlo.
            dto.Destinatarios = "Payload ilegible";
        }

        return dto;
    }
}
