using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace ReceptRegister.Api.Data;

public interface IDbConnectionFactory
{
    DbConnection Create();
}

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration config, IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "receptregister.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            ForeignKeys = true
        }.ToString();
    }

    public DbConnection Create() => new SqliteConnection(_connectionString);
}