using System.Data.Common;

namespace ReceptRegister.Api.Data;

public sealed class SqliteSchemaInitializer : ISchemaInitializer
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<SqliteSchemaInitializer> _logger;
    public SqliteSchemaInitializer(IDbConnectionFactory factory, ILogger<SqliteSchemaInitializer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Initializing SQLite schema...");
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS Recipes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Book TEXT NOT NULL,
                Page INTEGER NOT NULL,
                Notes TEXT NULL,
                Tried INTEGER NOT NULL DEFAULT 0
            );",
            @"CREATE TABLE IF NOT EXISTS AuthConfig (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                PasswordHash BLOB NOT NULL,
                Salt BLOB NOT NULL,
                Iterations INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );",
            @"CREATE TABLE IF NOT EXISTS Keywords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            );",
            @"CREATE TABLE IF NOT EXISTS RecipeCategories (
                RecipeId INTEGER NOT NULL,
                CategoryId INTEGER NOT NULL,
                PRIMARY KEY (RecipeId, CategoryId),
                FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
            );",
            @"CREATE TABLE IF NOT EXISTS RecipeKeywords (
                RecipeId INTEGER NOT NULL,
                KeywordId INTEGER NOT NULL,
                PRIMARY KEY (RecipeId, KeywordId),
                FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE,
                FOREIGN KEY (KeywordId) REFERENCES Keywords(Id) ON DELETE CASCADE
            );",
            "CREATE INDEX IF NOT EXISTS IX_Recipes_Name ON Recipes(Name);",
            "CREATE INDEX IF NOT EXISTS IX_Categories_Name ON Categories(Name);",
            "CREATE INDEX IF NOT EXISTS IX_Keywords_Name ON Keywords(Name);"
        };

        foreach (var sql in commands)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        _logger.LogDebug("SQLite schema initialization complete.");
    }
}
