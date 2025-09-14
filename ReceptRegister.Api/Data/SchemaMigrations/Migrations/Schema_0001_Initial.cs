namespace ReceptRegister.Api.Data.SchemaMigrations.Migrations;

// Initial schema creation (idempotent, guarded). Run once when MigrationHistory empty.
public sealed class Schema_0001_Initial : ISchemaMigration
{
    public int Id => 1;
    public string Name => "initial-schema";
    public string GetSql(string provider)
    {
        if (provider == "SqlServer")
        {
            return @"-- Initial tables
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Recipes') BEGIN
    CREATE TABLE dbo.Recipes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(400) NOT NULL,
        Book NVARCHAR(400) NOT NULL,
        Page INT NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        Tried BIT NOT NULL DEFAULT 0
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='AuthConfig') BEGIN
    CREATE TABLE dbo.AuthConfig (
        Id INT NOT NULL CONSTRAINT PK_AuthConfig PRIMARY KEY,
        PasswordHash VARBINARY(512) NOT NULL,
        Salt VARBINARY(256) NOT NULL,
        Iterations INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Categories') BEGIN
    CREATE TABLE dbo.Categories (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL UNIQUE
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Keywords') BEGIN
    CREATE TABLE dbo.Keywords (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL UNIQUE
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='RecipeCategories') BEGIN
    CREATE TABLE dbo.RecipeCategories (
        RecipeId INT NOT NULL,
        CategoryId INT NOT NULL,
        CONSTRAINT PK_RecipeCategories PRIMARY KEY (RecipeId, CategoryId),
        CONSTRAINT FK_RecipeCategories_Recipes FOREIGN KEY (RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeCategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE CASCADE
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='RecipeKeywords') BEGIN
    CREATE TABLE dbo.RecipeKeywords (
        RecipeId INT NOT NULL,
        KeywordId INT NOT NULL,
        CONSTRAINT PK_RecipeKeywords PRIMARY KEY (RecipeId, KeywordId),
        CONSTRAINT FK_RecipeKeywords_Recipes FOREIGN KEY (RecipeId) REFERENCES dbo.Recipes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeKeywords_Keywords FOREIGN KEY (KeywordId) REFERENCES dbo.Keywords(Id) ON DELETE CASCADE
    );
END
-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Recipes_Name') CREATE INDEX IX_Recipes_Name ON dbo.Recipes(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Categories_Name') CREATE INDEX IX_Categories_Name ON dbo.Categories(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Keywords_Name') CREATE INDEX IX_Keywords_Name ON dbo.Keywords(Name);";
        }
        // SQLite
        return @"CREATE TABLE IF NOT EXISTS Recipes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Book TEXT NOT NULL,
    Page INTEGER NOT NULL,
    Notes TEXT NULL,
    Tried INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS AuthConfig (
    Id INTEGER NOT NULL PRIMARY KEY,
    PasswordHash BLOB NOT NULL,
    Salt BLOB NOT NULL,
    Iterations INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Categories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);
CREATE TABLE IF NOT EXISTS Keywords (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);
CREATE TABLE IF NOT EXISTS RecipeCategories (
    RecipeId INTEGER NOT NULL,
    CategoryId INTEGER NOT NULL,
    PRIMARY KEY (RecipeId, CategoryId),
    FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS RecipeKeywords (
    RecipeId INTEGER NOT NULL,
    KeywordId INTEGER NOT NULL,
    PRIMARY KEY (RecipeId, KeywordId),
    FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE,
    FOREIGN KEY (KeywordId) REFERENCES Keywords(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Recipes_Name ON Recipes(Name);
CREATE INDEX IF NOT EXISTS IX_Categories_Name ON Categories(Name);
CREATE INDEX IF NOT EXISTS IX_Keywords_Name ON Keywords(Name);";
    }
}
