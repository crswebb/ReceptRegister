namespace ReceptRegister.Api.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Provider identifier. Allowed values currently: SQLite, SqlServer.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Connection string used when Provider = SqlServer. Ignored for SQLite.
    /// </summary>
    public string? ConnectionString { get; set; }
}

public static class DatabaseOptionsValidation
{
    public static void Validate(this DatabaseOptions options)
    {
        // Normalize provider (null -> SQLite default later)
        if (!string.IsNullOrWhiteSpace(options.Provider))
        {
            var p = options.Provider.Trim();
            if (p != "SQLite" && p != "SqlServer")
            {
                throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'. Expected: SQLite or SqlServer.");
            }
            if (p == "SqlServer" && string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("Database:ConnectionString must be provided when Database:Provider=SqlServer.");
            }
        }
    }
}
