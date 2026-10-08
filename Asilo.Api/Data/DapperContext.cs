using System.Data;
using Microsoft.Data.SqlClient;

namespace Asilo.Api.Data;

// Unica responsabilidad: fabricar conexiones a SQL Server.
// No guarda estado, por eso se registra como Singleton en Program.cs.
public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'DefaultConnection' en appsettings.json.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
