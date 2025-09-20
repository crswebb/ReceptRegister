using System.Data.Common;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;

namespace ReceptRegister.Api.Data;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly bool _useEntra;
    private readonly TokenCredential? _credential;
    private readonly ILogger<SqlServerConnectionFactory> _logger;

    public SqlServerConnectionFactory(IConfiguration configuration, ILogger<SqlServerConnectionFactory> logger)
    {
        _logger = logger;
        _connectionString = configuration["RECEPT_DB_CONNECTIONSTRING"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Database provider 'SqlServer' selected but RECEPT_DB_CONNECTIONSTRING is missing or empty.");

        // Heuristic: if connection string contains Authentication=Active Directory, or ActiveDirectory, or Aad access token pattern
        // we will use DefaultAzureCredential to fetch a token for https://database.windows.net/ scope.
        var lower = _connectionString.ToLowerInvariant();
        if (lower.Contains("authentication=activedirectory") || lower.Contains("authentication=active directory") || lower.Contains("active directory default"))
        {
            _useEntra = true;
            _credential = new DefaultAzureCredential();
        }
    }

    public DbConnection Create()
    {
        var conn = new SqlConnection(_connectionString);
        // Do not set AccessToken if the connection string already specifies any 'Authentication=' option.
        var hasAuthInConnectionString =
            _connectionString.IndexOf("authentication=", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!hasAuthInConnectionString && _useEntra && _credential is not null)
        {
            // Acquire token with a bounded timeout to avoid indefinite hangs on metadata endpoint issues.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var token = _credential.GetToken(new TokenRequestContext(new[] { "https://database.windows.net/.default" }), cts.Token);
                conn.AccessToken = token.Token;
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("[SQL] Using AccessToken via DefaultAzureCredential (no Authentication in connection string).");
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Timed out acquiring Entra ID access token for SQL Server within 15s.");
            }
        }
        else if (hasAuthInConnectionString)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[SQL] Using connection-string provided Authentication (no AccessToken set).");
            }
        }
        return conn;
    }
}
