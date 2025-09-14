using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace ReceptRegister.Api.Data.SchemaMigrations;

public sealed class SchemaMigrator : ISchemaMigrator
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<SchemaMigrator> _logger;
    private readonly DatabaseOptions _options;
    private readonly IReadOnlyList<ISchemaMigration> _migrations;

    public SchemaMigrator(IDbConnectionFactory factory, ILogger<SchemaMigrator> logger, DatabaseOptions options)
    {
        _factory = factory;
        _logger = logger;
        _options = options;
        _migrations = LoadMigrations();
    }

    public async Task<SchemaMigrationResult> MigrateAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var provider = _options.Provider ?? "SQLite"; // default
            await EnsureHistoryTableAsync(conn, provider, ct);
            var appliedIds = await LoadAppliedIdsAsync(conn, provider, ct);

            var ordered = _migrations.OrderBy(m => m.Id).ToList();
            int applied = 0, skipped = 0;
            foreach (var m in ordered)
            {
                if (appliedIds.Contains(m.Id))
                {
                    skipped++;
                    continue;
                }
                _logger.LogInformation("Applying migration {Id} {Name}...", m.Id, m.Name);
                await using var tx = await conn.BeginTransactionAsync(ct);
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = m.GetSql(provider);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await InsertHistoryAsync(conn, provider, m.Id, m.Name, ct, tx);
                await tx.CommitAsync(ct);
                applied++;
            }
            var msg = $"SchemaMigrator completed. Applied={applied}, Skipped={skipped}";
            _logger.LogInformation(msg);
            return new SchemaMigrationResult(applied, skipped, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema migration failed.");
            throw;
        }
    }

    private static IReadOnlyList<ISchemaMigration> LoadMigrations()
    {
        var list = new List<ISchemaMigration>();
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || !typeof(ISchemaMigration).IsAssignableFrom(type)) continue;
            if (Activator.CreateInstance(type) is ISchemaMigration inst) list.Add(inst);
        }
        return list;
    }

    private static async Task EnsureHistoryTableAsync(DbConnection conn, string provider, CancellationToken ct)
    {
        var sql = provider == "SqlServer" ?
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='MigrationHistory') BEGIN
                CREATE TABLE dbo.MigrationHistory (Id INT NOT NULL PRIMARY KEY, Name NVARCHAR(200) NOT NULL, AppliedAt DATETIMEOFFSET NOT NULL);
              END" :
            @"CREATE TABLE IF NOT EXISTS MigrationHistory (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, AppliedAt TEXT NOT NULL);";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<int>> LoadAppliedIdsAsync(DbConnection conn, string provider, CancellationToken ct)
    {
        var set = new HashSet<int>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = provider == "SqlServer" ? "SELECT Id FROM dbo.MigrationHistory" : "SELECT Id FROM MigrationHistory";
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) set.Add(reader.GetInt32(0));
        }
        catch { /* table might not exist yet (created earlier) */ }
        return set;
    }

    // Core table existence check no longer needed (migration 1 carries schema creation with IF NOT EXISTS guards)

    private static async Task InsertHistoryAsync(DbConnection conn, string provider, int id, string name, CancellationToken ct, DbTransaction? tx = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = provider == "SqlServer" ?
            "INSERT INTO dbo.MigrationHistory (Id, Name, AppliedAt) VALUES (@id, @name, SYSDATETIMEOFFSET())" :
            "INSERT INTO MigrationHistory (Id, Name, AppliedAt) VALUES (@id, @name, datetime('now'))";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@id"; p1.Value = id; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "@name"; p2.Value = name; cmd.Parameters.Add(p2);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
