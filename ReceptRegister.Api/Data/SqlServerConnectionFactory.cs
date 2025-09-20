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
    private readonly int _tokenTimeoutSeconds = 30; // default

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
            // Configure credential chain to avoid slow developer/interactive sources and IMDS when not on Azure.
            var isManagedIdentityEnv =
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT")) ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT"));

            var opts = new DefaultAzureCredentialOptions
            {
                ExcludeAzureCliCredential = true,
                ExcludeAzureDeveloperCliCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                // Use Managed Identity only when the environment signals MI is available to avoid IMDS timeouts.
                ExcludeManagedIdentityCredential = !isManagedIdentityEnv,
                // Always allow environment credential so service principal / workload identity works quickly in CI.
                ExcludeEnvironmentCredential = false
            };

            // If a specific client ID for MI is provided, honor it.
            var miClientId = configuration["RECEPT_DB_MANAGED_IDENTITY_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(miClientId))
            {
                opts.ManagedIdentityClientId = miClientId;
            }

            _credential = new DefaultAzureCredential(opts);

            // Timeout configuration: env overrides > config > default
            if (int.TryParse(Environment.GetEnvironmentVariable("RECEPT_DB_AAD_TOKEN_TIMEOUT_SECONDS"), out var fromEnv) && fromEnv > 0)
            {
                _tokenTimeoutSeconds = fromEnv;
            }
            else if (int.TryParse(configuration["Database:EntraTokenTimeoutSeconds"], out var fromCfg) && fromCfg > 0)
            {
                _tokenTimeoutSeconds = fromCfg;
            }
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
            var scope = new[] { "https://database.windows.net/.default" };
            var attempt = 0;
            var maxAttempts = 2; // one retry on timeout/transient
            while (true)
            {
                attempt++;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_tokenTimeoutSeconds));
                try
                {
                    var token = _credential.GetToken(new TokenRequestContext(scope), cts.Token);
                    conn.AccessToken = token.Token;
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("[SQL] Using AccessToken via DefaultAzureCredential (no Authentication in connection string).");
                    }
                    break; // success
                }
                catch (OperationCanceledException) when (attempt < maxAttempts)
                {
                    // retry once in case of transient IMDS slowness
                    continue;
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Timed out acquiring Entra ID access token for SQL Server within {_tokenTimeoutSeconds}s. " +
                        "If running outside Azure, consider disabling Managed Identity (set ExcludeManagedIdentity or remove IDENTITY_ENDPOINT/MSI_ENDPOINT). " +
                        "You can also increase timeout via RECEPT_DB_AAD_TOKEN_TIMEOUT_SECONDS or Database:EntraTokenTimeoutSeconds.");
                }
                catch (AuthenticationFailedException ex)
                {
                    throw new InvalidOperationException("Failed to acquire Entra ID token for SQL Server using DefaultAzureCredential. " +
                        "Ensure appropriate environment variables or managed identity are configured.", ex);
                }
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
