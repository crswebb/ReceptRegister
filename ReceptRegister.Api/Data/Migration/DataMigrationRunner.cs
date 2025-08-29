using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace ReceptRegister.Api.Data.Migration;

/// <summary>
/// Performs a one-shot migration from a legacy / existing SQLite database file to the currently configured target provider (SQL Server).
/// Only runs when invoked via --migrate-sqlite=<path> with Database:Provider=SqlServer.
/// Safe to re-run: it will upsert taxonomy terms and skip existing recipes by (Name, Book, Page) triple to avoid duplicates.
/// </summary>
public sealed class DataMigrationRunner
{
    private readonly IDbConnectionFactory _targetFactory;
    private readonly ILogger<DataMigrationRunner> _logger;

    public DataMigrationRunner(IDbConnectionFactory targetFactory, ILogger<DataMigrationRunner> logger)
    {
        _targetFactory = targetFactory;
        _logger = logger;
    }

    public sealed record MigrationResult(bool Success, string Message, int Recipes, int Categories, int Keywords);

    public async Task<MigrationResult> MigrateFromSqliteAsync(string sqlitePath, CancellationToken ct)
    {
        if (!File.Exists(sqlitePath))
            return new MigrationResult(false, $"SQLite file not found: {sqlitePath}", 0,0,0);

        _logger.LogInformation("Starting migration from SQLite file {File}", sqlitePath);

        // Open read-only SQLite connection
        var sqliteCs = new SqliteConnectionStringBuilder { DataSource = sqlitePath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        await using var sourceConn = new SqliteConnection(sqliteCs);
        await sourceConn.OpenAsync(ct);

        // Ensure target schema exists
        await using var targetConn = _targetFactory.Create();
        await targetConn.OpenAsync(ct);

        // Load taxonomy first (names lowercase already in schema usage)
        var categories = await LoadNamesAsync(sourceConn, "Categories", ct);
        var keywords = await LoadNamesAsync(sourceConn, "Keywords", ct);

        int insertedCategories = 0, insertedKeywords = 0;
        foreach (var c in categories) insertedCategories += await UpsertTaxonomyAsync(targetConn, "Categories", c, ct);
        foreach (var k in keywords) insertedKeywords += await UpsertTaxonomyAsync(targetConn, "Keywords", k, ct);

        // Cache taxonomy name->id after upsert
        var categoryIds = await LoadNameIdsAsync(targetConn, "Categories", ct);
        var keywordIds = await LoadNameIdsAsync(targetConn, "Keywords", ct);

        // Load all recipes with linked taxonomy
        var recipeCmdText = @"SELECT r.Id, r.Name, r.Book, r.Page, r.Notes, r.Tried,
              (SELECT group_concat(c.Name,'|') FROM Categories c JOIN RecipeCategories rc ON rc.CategoryId=c.Id WHERE rc.RecipeId=r.Id) AS CatNames,
              (SELECT group_concat(k.Name,'|') FROM Keywords k JOIN RecipeKeywords rk ON rk.KeywordId=k.Id WHERE rk.RecipeId=r.Id) AS KeyNames
            FROM Recipes r";
        var recipes = new List<(int Id,string Name,string Book,int Page,string? Notes,bool Tried,string[] Categories,string[] Keywords)>();
        await using (var cmd = sourceConn.CreateCommand())
        {
            cmd.CommandText = recipeCmdText;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var catNames = (reader[6] as string)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                var keyNames = (reader[7] as string)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                recipes.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.IsDBNull(4)? null : reader.GetString(4), reader.GetBoolean(5), catNames, keyNames));
            }
        }

        int migratedRecipes = 0; int skipped = 0;
        foreach (var r in recipes)
        {
            // Detect existing by natural key (Name+Book+Page)
            if (await ExistsAsync(targetConn, r.Name, r.Book, r.Page, ct)) { skipped++; continue; }
            int newId = await InsertRecipeAsync(targetConn, r, ct);
            foreach (var cn in r.Categories)
                if (categoryIds.TryGetValue(cn, out var cid)) await AttachAsync(targetConn, "RecipeCategories", newId, cid, ct);
            foreach (var kn in r.Keywords)
                if (keywordIds.TryGetValue(kn, out var kid)) await AttachAsync(targetConn, "RecipeKeywords", newId, kid, ct);
            migratedRecipes++;
        }

        return new MigrationResult(true, $"Migrated {migratedRecipes} recipes (skipped {skipped} existing). Categories upserted: {insertedCategories}, Keywords upserted: {insertedKeywords}.", migratedRecipes, insertedCategories, insertedKeywords);
    }

    private static async Task<List<string>> LoadNamesAsync(DbConnection conn, string table, CancellationToken ct)
    {
        var list = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Name FROM {table}";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(reader.GetString(0));
        return list;
    }

    private static async Task<int> UpsertTaxonomyAsync(DbConnection conn, string table, string name, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE Name=@n) INSERT INTO {table}(Name) VALUES (@n);"; // Works on SQL Server only.
        var p = cmd.CreateParameter(); p.ParameterName = "@n"; p.Value = name; cmd.Parameters.Add(p);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Dictionary<string,int>> LoadNameIdsAsync(DbConnection conn, string table, CancellationToken ct)
    {
        var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name FROM {table}";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) dict[reader.GetString(1)] = reader.GetInt32(0);
        return dict;
    }

    private static async Task<bool> ExistsAsync(DbConnection conn, string name, string book, int page, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Recipes WHERE Name=@n AND Book=@b AND Page=@p";
        Add(cmd,"@n",name); Add(cmd,"@b",book); Add(cmd,"@p",page);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    private static async Task<int> InsertRecipeAsync(DbConnection conn, (int Id,string Name,string Book,int Page,string? Notes,bool Tried,string[] Categories,string[] Keywords) r, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Recipes(Name,Book,Page,Notes,Tried) VALUES (@n,@b,@p,@no,@t);SELECT SCOPE_IDENTITY();";
        Add(cmd,"@n",r.Name); Add(cmd,"@b",r.Book); Add(cmd,"@p",r.Page); Add(cmd,"@no", (object?)r.Notes ?? DBNull.Value); Add(cmd,"@t", r.Tried);
        var idObj = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(idObj);
    }

    private static async Task AttachAsync(DbConnection conn, string table, int recipeId, int taxonomyId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE RecipeId=@r AND {(table.EndsWith("Categories")?"CategoryId":"KeywordId")}=@x) INSERT INTO {table}(RecipeId,{(table.EndsWith("Categories")?"CategoryId":"KeywordId")}) VALUES (@r,@x);";
        Add(cmd,"@r",recipeId); Add(cmd,"@x",taxonomyId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Add(DbCommand cmd, string name, object value)
    { var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value; cmd.Parameters.Add(p); }
}
