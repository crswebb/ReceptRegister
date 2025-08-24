namespace ReceptRegister.Api.Data;

public sealed class SqlServerSchemaInitializer : ISchemaInitializer
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<SqlServerSchemaInitializer> _logger;
    public SqlServerSchemaInitializer(IDbConnectionFactory factory, ILogger<SqlServerSchemaInitializer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // NOTE: This is a first pass placeholder. Full parity (constraints, cascades, indexes) will be refined in #107.
        _logger.LogDebug("Initializing SQL Server schema (experimental)...");
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var commands = new[]
        {
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Recipes') BEGIN
                CREATE TABLE dbo.Recipes (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(400) NOT NULL,
                    Book NVARCHAR(400) NOT NULL,
                    Page INT NOT NULL,
                    Notes NVARCHAR(MAX) NULL,
                    Tried BIT NOT NULL DEFAULT 0
                );
            END",
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuthConfig') BEGIN
                CREATE TABLE dbo.AuthConfig (
                    Id INT NOT NULL CONSTRAINT PK_AuthConfig PRIMARY KEY,
                    PasswordHash VARBINARY(512) NOT NULL,
                    Salt VARBINARY(256) NOT NULL,
                    Iterations INT NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL,
                    UpdatedAt DATETIMEOFFSET NOT NULL
                );
            END",
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories') BEGIN
                CREATE TABLE dbo.Categories (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL UNIQUE
                );
            END",
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Keywords') BEGIN
                CREATE TABLE dbo.Keywords (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL UNIQUE
                );
            END",
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RecipeCategories') BEGIN
                CREATE TABLE dbo.RecipeCategories (
                    RecipeId INT NOT NULL,
                    CategoryId INT NOT NULL,
                    CONSTRAINT PK_RecipeCategories PRIMARY KEY (RecipeId, CategoryId),
                    CONSTRAINT FK_RecipeCategories_Recipes FOREIGN KEY (RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_RecipeCategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE CASCADE
                );
            END",
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RecipeKeywords') BEGIN
                CREATE TABLE dbo.RecipeKeywords (
                    RecipeId INT NOT NULL,
                    KeywordId INT NOT NULL,
                    CONSTRAINT PK_RecipeKeywords PRIMARY KEY (RecipeId, KeywordId),
                    CONSTRAINT FK_RecipeKeywords_Recipes FOREIGN KEY (RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_RecipeKeywords_Keywords FOREIGN KEY (KeywordId) REFERENCES dbo.Keywords(Id) ON DELETE CASCADE
                );
            END",
            // Indexes
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Recipes_Name') CREATE INDEX IX_Recipes_Name ON dbo.Recipes(Name);",
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Categories_Name') CREATE INDEX IX_Categories_Name ON dbo.Categories(Name);",
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Keywords_Name') CREATE INDEX IX_Keywords_Name ON dbo.Keywords(Name);"
        };

        foreach (var sql in commands)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        _logger.LogDebug("SQL Server schema initialization complete (experimental).");
    }
}
