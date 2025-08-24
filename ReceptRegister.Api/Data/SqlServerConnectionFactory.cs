using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace ReceptRegister.Api.Data;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration["Database:ConnectionString"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Database provider 'SqlServer' selected but Database:ConnectionString is missing or empty.");
        }
    }

    public DbConnection Create() => new SqlConnection(_connectionString);
}
