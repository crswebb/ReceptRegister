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
            // Accept case-insensitive values
            if (string.Equals(p, "sqlite", StringComparison.OrdinalIgnoreCase)) p = "SQLite";
            else if (string.Equals(p, "sqlserver", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "mssql", StringComparison.OrdinalIgnoreCase)) p = "SqlServer";
            else if (p != "SQLite" && p != "SqlServer")
            {
                throw new InvalidOperationException($"Unsupported database provider '{options.Provider}'. Expected: SQLite or SqlServer.");
            }
            options.Provider = p; // normalize
            if (p == "SqlServer" && string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("Database:ConnectionString must be provided when Database:Provider=SqlServer.");
            }
        }
    }
}
