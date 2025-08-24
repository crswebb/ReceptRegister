namespace ReceptRegister.Api.Data;

public interface IDatabaseDialect
{
    string Name { get; }
    string InsertRecipeSql { get; } // returns insert + select identity
    string BuildInsertIgnoreTaxonomySql(string table); // expects @n parameter
    string BuildInsertIgnoreLinkSql(string linkTable, string fkColumn); // expects @r, @t parameters
    string BuildPagedClause(); // expects @ps (page size) & @off (offset)
    bool IsSqlServer { get; }
}

public sealed class SqliteDialect : IDatabaseDialect
{
    public string Name => "SQLite";
    public bool IsSqlServer => false;
    public string InsertRecipeSql => "INSERT INTO Recipes (Name, Book, Page, Notes, Tried) VALUES (@n,@b,@p,@no,@t); SELECT last_insert_rowid();";
    public string BuildInsertIgnoreTaxonomySql(string table) => $"INSERT INTO {table} (Name) VALUES (@n) ON CONFLICT(Name) DO NOTHING;";
    public string BuildInsertIgnoreLinkSql(string linkTable, string fkColumn) => $"INSERT OR IGNORE INTO {linkTable} (RecipeId,{fkColumn}) VALUES (@r,@t);";
    public string BuildPagedClause() => "LIMIT @ps OFFSET @off";
}

public sealed class SqlServerDialect : IDatabaseDialect
{
    public string Name => "SqlServer";
    public bool IsSqlServer => true;
    public string InsertRecipeSql => "INSERT INTO Recipes (Name, Book, Page, Notes, Tried) VALUES (@n,@b,@p,@no,@t); SELECT CAST(SCOPE_IDENTITY() AS int);";
    public string BuildInsertIgnoreTaxonomySql(string table) => $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE Name=@n) INSERT INTO {table} (Name) VALUES (@n);";
    public string BuildInsertIgnoreLinkSql(string linkTable, string fkColumn) => $"IF NOT EXISTS (SELECT 1 FROM {linkTable} WHERE RecipeId=@r AND {fkColumn}=@t) INSERT INTO {linkTable} (RecipeId,{fkColumn}) VALUES (@r,@t);";
    public string BuildPagedClause() => "OFFSET @off ROWS FETCH NEXT @ps ROWS ONLY";
}
