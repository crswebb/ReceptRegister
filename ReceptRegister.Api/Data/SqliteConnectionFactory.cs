using Microsoft.Data.Sqlite;

namespace ReceptRegister.Api.Data;

public interface ISqliteConnectionFactory
{
    SqliteConnection Create();
}

public class SqliteConnectionFactory : ISqliteConnectionFactory
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

    public SqliteConnection Create()
    {
        var conn = new SqliteConnection(_connectionString);
        return conn;
    }
}