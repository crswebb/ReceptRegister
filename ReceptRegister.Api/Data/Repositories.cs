using Microsoft.Data.Sqlite;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Api.Data;

public interface IRecipesRepository
{
    Task<int> AddAsync(Recipe recipe, IEnumerable<string> categories, IEnumerable<string> keywords, CancellationToken ct = default);
    Task<Recipe?> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(Recipe recipe, IEnumerable<string> categories, IEnumerable<string> keywords, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task ToggleTriedAsync(int id, bool tried, CancellationToken ct = default);
    Task<(IReadOnlyList<Recipe> Items, int Total)> SearchAsync(RecipeSearchCriteria criteria, CancellationToken ct = default);
    Task<IReadOnlyList<Recipe>> LegacySearchAsync(string? term, CancellationToken ct = default); // temporary for frontend until enhanced wired
    Task<bool> AttachCategoryAsync(int recipeId, int categoryId, CancellationToken ct = default);
    Task<bool> DetachCategoryAsync(int recipeId, int categoryId, CancellationToken ct = default);
    Task<bool> AttachKeywordAsync(int recipeId, int keywordId, CancellationToken ct = default);
    Task<bool> DetachKeywordAsync(int recipeId, int keywordId, CancellationToken ct = default);
}

public interface ITaxonomyRepository
{
    Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Keyword>> ListKeywordsAsync(CancellationToken ct = default);
}

public interface IAuthRepository
{
    Task<bool> HasPasswordAsync(CancellationToken ct = default);
    Task<AuthConfig?> GetAsync(CancellationToken ct = default);
    Task SetPasswordAsync(byte[] hash, byte[] salt, int iterations, DateTimeOffset now, CancellationToken ct = default);
}

public sealed record AuthConfig(byte[] PasswordHash, byte[] Salt, int Iterations, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public class AuthRepository : IAuthRepository
{
    private readonly ISqliteConnectionFactory _factory;
    public AuthRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<bool> HasPasswordAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM AuthConfig WHERE Id=1";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task<AuthConfig?> GetAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash, Salt, Iterations, CreatedAt, UpdatedAt FROM AuthConfig WHERE Id=1";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AuthConfig(
            (byte[])reader[0],
            (byte[])reader[1],
            reader.GetInt32(2),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4))
        );
    }

    public async Task SetPasswordAsync(byte[] hash, byte[] salt, int iterations, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var existsCmd = conn.CreateCommand();
        existsCmd.CommandText = "SELECT 1 FROM AuthConfig WHERE Id=1";
        var exists = await existsCmd.ExecuteScalarAsync(ct) is not null;
        if (!exists)
        {
            var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO AuthConfig (Id, PasswordHash, Salt, Iterations, CreatedAt, UpdatedAt) VALUES (1,$h,$s,$it,$c,$u)";
            insert.Parameters.AddWithValue("$h", hash);
            insert.Parameters.AddWithValue("$s", salt);
            insert.Parameters.AddWithValue("$it", iterations);
            insert.Parameters.AddWithValue("$c", now.ToString("O"));
            insert.Parameters.AddWithValue("$u", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(ct);
        }
        else
        {
            var update = conn.CreateCommand();
            update.CommandText = "UPDATE AuthConfig SET PasswordHash=$h, Salt=$s, Iterations=$it, UpdatedAt=$u WHERE Id=1";
            update.Parameters.AddWithValue("$h", hash);
            update.Parameters.AddWithValue("$s", salt);
            update.Parameters.AddWithValue("$it", iterations);
            update.Parameters.AddWithValue("$u", now.ToString("O"));
            await update.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}

public class RecipesRepository : IRecipesRepository
{
    private readonly ISqliteConnectionFactory _factory;
    public RecipesRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> AddAsync(Recipe recipe, IEnumerable<string> categories, IEnumerable<string> keywords, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"INSERT INTO Recipes (Name, Book, Page, Notes, Tried) VALUES ($n,$b,$p,$no,$t); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("$n", recipe.Name);
        insertCmd.Parameters.AddWithValue("$b", recipe.Book);
        insertCmd.Parameters.AddWithValue("$p", recipe.Page);
        insertCmd.Parameters.AddWithValue("$no", (object?)recipe.Notes ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$t", recipe.Tried ? 1 : 0);
        var idObj = await insertCmd.ExecuteScalarAsync(ct);
        if (idObj is null)
            throw new InvalidOperationException("Failed to retrieve new recipe id");
        var id = Convert.ToInt64(idObj);
        recipe.Id = checked((int)id);

        await UpsertTaxonomyAndLink(conn, "Categories", "RecipeCategories", "CategoryId", recipe.Id, categories, ct);
        await UpsertTaxonomyAndLink(conn, "Keywords", "RecipeKeywords", "KeywordId", recipe.Id, keywords, ct);

        await tx.CommitAsync(ct);
        return recipe.Id;
    }

    public async Task<Recipe?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var recipe = await GetCoreRecipe(conn, id, ct);
        if (recipe == null) return null;
        await LoadJoins(conn, recipe, ct);
        return recipe;
    }

    public async Task UpdateAsync(Recipe recipe, IEnumerable<string> categories, IEnumerable<string> keywords, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE Recipes SET Name=$n, Book=$b, Page=$p, Notes=$no, Tried=$t WHERE Id=$id";
        cmd.Parameters.AddWithValue("$n", recipe.Name);
        cmd.Parameters.AddWithValue("$b", recipe.Book);
        cmd.Parameters.AddWithValue("$p", recipe.Page);
        cmd.Parameters.AddWithValue("$no", (object?)recipe.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", recipe.Tried ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", recipe.Id);
        await cmd.ExecuteNonQueryAsync(ct);

        await ReplaceLinks(conn, recipe.Id, "RecipeCategories", categories, "Categories", "CategoryId", ct);
        await ReplaceLinks(conn, recipe.Id, "RecipeKeywords", keywords, "Keywords", "KeywordId", ct);

        await tx.CommitAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Recipes WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ToggleTriedAsync(int id, bool tried, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Recipes SET Tried=$t WHERE Id=$id";
        cmd.Parameters.AddWithValue("$t", tried ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Recipe>> LegacySearchAsync(string? term, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var list = new List<Recipe>();
        var cmd = conn.CreateCommand();
        if (string.IsNullOrWhiteSpace(term))
        {
            cmd.CommandText = "SELECT Id, Name, Book, Page, Notes, Tried FROM Recipes ORDER BY Name LIMIT 200";
        }
        else
        {
            cmd.CommandText = @"SELECT DISTINCT r.Id, r.Name, r.Book, r.Page, r.Notes, r.Tried
                               FROM Recipes r
                               LEFT JOIN RecipeCategories rc ON rc.RecipeId=r.Id
                               LEFT JOIN Categories c ON c.Id=rc.CategoryId
                               LEFT JOIN RecipeKeywords rk ON rk.RecipeId=r.Id
                               LEFT JOIN Keywords k ON k.Id=rk.KeywordId
                               WHERE r.Name LIKE $q OR r.Book LIKE $q OR c.Name LIKE $q OR k.Name LIKE $q
                               ORDER BY r.Name LIMIT 200";
            cmd.Parameters.AddWithValue("$q", $"%{term}%");
        }
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Book = reader.GetString(2),
                Page = reader.GetInt32(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Tried = reader.GetInt32(5) == 1
            });
        }

        // Load joins for each (N+1 ok for small initial scope; can optimize later)
        foreach (var r in list)
            await LoadJoins(conn, r, ct);

        return list;
    }

    public async Task<(IReadOnlyList<Recipe> Items, int Total)> SearchAsync(RecipeSearchCriteria criteria, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        // Build dynamic WHERE clauses
        var where = new List<string>();
        var parameters = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            where.Add(@"(r.Name LIKE $q OR r.Book LIKE $q OR r.Notes LIKE $q OR c.Name LIKE $q OR k.Name LIKE $q)");
            parameters["$q"] = $"%{criteria.Query}%";
        }
        if (!string.IsNullOrWhiteSpace(criteria.Book))
        {
            where.Add("r.Book = $book");
            parameters["$book"] = criteria.Book.Trim();
        }
        if (criteria.Tried is not null)
        {
            where.Add("r.Tried = $tried");
            parameters["$tried"] = criteria.Tried.Value ? 1 : 0;
        }
        if (criteria.CategoryIds.Count > 0)
        {
            where.Add($"r.Id IN (SELECT rc.RecipeId FROM RecipeCategories rc WHERE rc.CategoryId IN ({string.Join(',', criteria.CategoryIds)}))");
        }
        if (criteria.KeywordIds.Count > 0)
        {
            where.Add($"r.Id IN (SELECT rk.RecipeId FROM RecipeKeywords rk WHERE rk.KeywordId IN ({string.Join(',', criteria.KeywordIds)}))");
        }

        var whereSql = where.Count == 0 ? string.Empty : ("WHERE " + string.Join(" AND ", where));

        // Count total
        var countCmd = conn.CreateCommand();
        countCmd.CommandText = $@"SELECT COUNT(DISTINCT r.Id) FROM Recipes r
LEFT JOIN RecipeCategories rc ON rc.RecipeId=r.Id
LEFT JOIN Categories c ON c.Id=rc.CategoryId
LEFT JOIN RecipeKeywords rk ON rk.RecipeId=r.Id
LEFT JOIN Keywords k ON k.Id=rk.KeywordId
{whereSql}";
        foreach (var p in parameters)
            countCmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        var totalObj = await countCmd.ExecuteScalarAsync(ct);
        var total = totalObj is null ? 0 : Convert.ToInt32(totalObj);

        var offset = (criteria.Page - 1) * criteria.PageSize;
        var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"SELECT DISTINCT r.Id, r.Name, r.Book, r.Page, r.Notes, r.Tried FROM Recipes r
LEFT JOIN RecipeCategories rc ON rc.RecipeId=r.Id
LEFT JOIN Categories c ON c.Id=rc.CategoryId
LEFT JOIN RecipeKeywords rk ON rk.RecipeId=r.Id
LEFT JOIN Keywords k ON k.Id=rk.KeywordId
{whereSql}
ORDER BY r.Name
LIMIT $ps OFFSET $off";
        foreach (var p in parameters)
            dataCmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        dataCmd.Parameters.AddWithValue("$ps", criteria.PageSize);
        dataCmd.Parameters.AddWithValue("$off", offset);

        var list = new List<Recipe>();
        await using (var reader = await dataCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                list.Add(new Recipe
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Book = reader.GetString(2),
                    Page = reader.GetInt32(3),
                    Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Tried = reader.GetInt32(5) == 1
                });
            }
        }
        foreach (var r in list)
            await LoadJoins(conn, r, ct);

        return (list, total);
    }

    public async Task<bool> AttachCategoryAsync(int recipeId, int categoryId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        // Ensure recipe & category exist
        if (!await Exists(conn, "Recipes", recipeId, ct) || !await Exists(conn, "Categories", categoryId, ct)) return false;
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO RecipeCategories (RecipeId, CategoryId) VALUES ($r,$c)";
        cmd.Parameters.AddWithValue("$r", recipeId);
        cmd.Parameters.AddWithValue("$c", categoryId);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> DetachCategoryAsync(int recipeId, int categoryId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        if (!await Exists(conn, "Recipes", recipeId, ct) || !await Exists(conn, "Categories", categoryId, ct)) return false;
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM RecipeCategories WHERE RecipeId=$r AND CategoryId=$c";
        cmd.Parameters.AddWithValue("$r", recipeId);
        cmd.Parameters.AddWithValue("$c", categoryId);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> AttachKeywordAsync(int recipeId, int keywordId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        if (!await Exists(conn, "Recipes", recipeId, ct) || !await Exists(conn, "Keywords", keywordId, ct)) return false;
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO RecipeKeywords (RecipeId, KeywordId) VALUES ($r,$k)";
        cmd.Parameters.AddWithValue("$r", recipeId);
        cmd.Parameters.AddWithValue("$k", keywordId);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> DetachKeywordAsync(int recipeId, int keywordId, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        if (!await Exists(conn, "Recipes", recipeId, ct) || !await Exists(conn, "Keywords", keywordId, ct)) return false;
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM RecipeKeywords WHERE RecipeId=$r AND KeywordId=$k";
        cmd.Parameters.AddWithValue("$r", recipeId);
        cmd.Parameters.AddWithValue("$k", keywordId);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<Recipe?> GetCoreRecipe(SqliteConnection conn, int id, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Book, Page, Notes, Tried FROM Recipes WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Recipe
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Book = reader.GetString(2),
            Page = reader.GetInt32(3),
            Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
            Tried = reader.GetInt32(5) == 1
        };
    }

    private static async Task LoadJoins(SqliteConnection conn, Recipe recipe, CancellationToken ct)
    {
        // Categories
        var catCmd = conn.CreateCommand();
        catCmd.CommandText = @"SELECT c.Id, c.Name FROM Categories c
                               INNER JOIN RecipeCategories rc ON rc.CategoryId=c.Id
                               WHERE rc.RecipeId=$id ORDER BY c.Name";
        catCmd.Parameters.AddWithValue("$id", recipe.Id);
        await using (var reader = await catCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                recipe.Categories.Add(new Category(reader.GetInt32(0), reader.GetString(1)));
        }

        // Keywords
        var keyCmd = conn.CreateCommand();
        keyCmd.CommandText = @"SELECT k.Id, k.Name FROM Keywords k
                               INNER JOIN RecipeKeywords rk ON rk.KeywordId=k.Id
                               WHERE rk.RecipeId=$id ORDER BY k.Name";
        keyCmd.Parameters.AddWithValue("$id", recipe.Id);
        await using (var reader = await keyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                recipe.Keywords.Add(new Keyword(reader.GetInt32(0), reader.GetString(1)));
        }
    }

    private static async Task UpsertTaxonomyAndLink(SqliteConnection conn, string table, string linkTable, string linkFkName, int recipeId, IEnumerable<string> names, CancellationToken ct)
    {
        foreach (var raw in names.Select(n => n.Trim()).Where(n => n.Length > 0))
        {
            var name = raw.ToLowerInvariant(); // uniqueness case-insensitive
            // Upsert pattern: try insert, ignore conflict, then select id
            var insert = conn.CreateCommand();
            insert.CommandText = $"INSERT INTO {table} (Name) VALUES ($n) ON CONFLICT(Name) DO NOTHING;";
            insert.Parameters.AddWithValue("$n", name);
            await insert.ExecuteNonQueryAsync(ct);
            var sel = conn.CreateCommand();
            sel.CommandText = $"SELECT Id FROM {table} WHERE Name=$n";
            sel.Parameters.AddWithValue("$n", name);
            var idObj = await sel.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException($"Failed to resolve Id for {table} name '{name}'");
            var id = Convert.ToInt32(idObj);

            var link = conn.CreateCommand();
            link.CommandText = $"INSERT OR IGNORE INTO {linkTable} (RecipeId, {linkFkName}) VALUES ($r,$t)";
            link.Parameters.AddWithValue("$r", recipeId);
            link.Parameters.AddWithValue("$t", id);
            await link.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task ReplaceLinks(SqliteConnection conn, int recipeId, string linkTable, IEnumerable<string> names, string lookupTable, string linkFkName, CancellationToken ct)
    {
        // Clear existing
        var del = conn.CreateCommand();
        del.CommandText = $"DELETE FROM {linkTable} WHERE RecipeId=$r";
        del.Parameters.AddWithValue("$r", recipeId);
        await del.ExecuteNonQueryAsync(ct);

        await UpsertTaxonomyAndLink(conn, lookupTable, linkTable, linkFkName, recipeId, names, ct);
    }

    private static async Task<bool> Exists(SqliteConnection conn, string table, int id, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {table} WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }
}

public sealed record RecipeSearchCriteria(string? Query, string? Book, IReadOnlyList<int> CategoryIds, IReadOnlyList<int> KeywordIds, bool? Tried, int Page, int PageSize)
{
    public static RecipeSearchCriteria Create(
        string? query, string? book, IEnumerable<int>? categories, IEnumerable<int>? keywords, bool? tried, int? page, int? pageSize)
    {
        var p = page.GetValueOrDefault(1);
        if (p < 1) p = 1;
        var ps = pageSize.GetValueOrDefault(20);
        if (ps < 1) ps = 1; else if (ps > 100) ps = 100;
        return new RecipeSearchCriteria(query?.Trim(), string.IsNullOrWhiteSpace(book) ? null : book.Trim(),
            (categories ?? Array.Empty<int>()).Distinct().ToArray(),
            (keywords ?? Array.Empty<int>()).Distinct().ToArray(),
            tried,
            p, ps);
    }
}

public class TaxonomyRepository : ITaxonomyRepository
{
    private readonly ISqliteConnectionFactory _factory;
    public TaxonomyRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Categories ORDER BY Name";
        var list = new List<Category>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new Category(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public async Task<IReadOnlyList<Keyword>> ListKeywordsAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Keywords ORDER BY Name";
        var list = new List<Keyword>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new Keyword(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }
}