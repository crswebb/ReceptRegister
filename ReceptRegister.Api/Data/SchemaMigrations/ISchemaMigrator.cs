namespace ReceptRegister.Api.Data.SchemaMigrations;

public interface ISchemaMigration
{
    // Sequential numeric identifier (>1; 1 is reserved for baseline detection)
    int Id { get; }
    string Name { get; }
    string GetSql(string provider); // provider: "SqlServer" or "SQLite" (case-sensitive choices used in code)
}

public interface ISchemaMigrator
{
    Task<SchemaMigrationResult> MigrateAsync(CancellationToken ct = default);
}

public sealed record SchemaMigrationResult(int Applied, int Skipped, string? Message);
