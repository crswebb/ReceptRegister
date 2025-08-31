using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Localization;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Frontend.Pages;

public class DebugModel : PageModel
{
    public string CurrentCulture { get; private set; } = string.Empty;
    public string CurrentUICulture { get; private set; } = string.Empty;
    public string DefaultCulture { get; private set; } = string.Empty;
    public IReadOnlyList<string> SupportedCultures { get; private set; } = Array.Empty<string>();
    public string? EnvDefaultCulture { get; private set; }
    public string? EnvSupportedCultures { get; private set; }

    public string DatabaseProvider { get; private set; } = string.Empty;
    public string? ConfigConnectionString { get; private set; }
    public string ResolvedConnectionString { get; private set; } = string.Empty;
    public string? EnvDbProvider { get; private set; }
    public string? EnvDbConnectionString { get; private set; }

    public string EnvironmentName { get; private set; } = string.Empty;
    public string ContentRoot { get; private set; } = string.Empty;
    public string AssemblyVersion { get; private set; } = string.Empty;
    public DateTime UtcNow { get; private set; }

    private readonly DatabaseOptions _dbOptions;
    private readonly IDbConnectionFactory _connFactory;
    private readonly IWebHostEnvironment _env;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<RequestLocalizationOptions> _locOpts;
    private readonly IConfiguration _configuration;

    public DebugModel(DatabaseOptions dbOptions,
        IDbConnectionFactory connFactory,
        IWebHostEnvironment env,
        TimeProvider timeProvider,
        IOptions<RequestLocalizationOptions> locOpts,
        IConfiguration configuration)
    {
        _dbOptions = dbOptions;
        _connFactory = connFactory;
        _env = env;
        _timeProvider = timeProvider;
        _locOpts = locOpts;
        _configuration = configuration;
    }

    public void OnGet()
    {
        CurrentCulture = CultureInfo.CurrentCulture.Name;
        CurrentUICulture = CultureInfo.CurrentUICulture.Name;
        DefaultCulture = _locOpts.Value.DefaultRequestCulture.Culture.Name;
        SupportedCultures = _locOpts.Value.SupportedCultures?.Select(c => c.Name).ToArray() ?? Array.Empty<string>();
        EnvDefaultCulture = Environment.GetEnvironmentVariable("RECEPT_DEFAULT_CULTURE");
        EnvSupportedCultures = Environment.GetEnvironmentVariable("RECEPT_SUPPORTED_CULTURES");

        DatabaseProvider = _dbOptions.Provider ?? "SQLite (default)";
        ConfigConnectionString = _configuration["Database:ConnectionString"];
        EnvDbProvider = Environment.GetEnvironmentVariable("RECEPT_DB_PROVIDER");
        EnvDbConnectionString = Environment.GetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING")
            ?? Environment.GetEnvironmentVariable("RECEPT_DB_CONNECTION");

        // Resolve actual connection string safely: attempt a connection object creation but do not open.
        try
        {
            using var c = _connFactory.Create();
            ResolvedConnectionString = c.ConnectionString;
        }
        catch (Exception ex)
        {
            ResolvedConnectionString = $"(error building connection: {ex.Message})";
        }

        EnvironmentName = _env.EnvironmentName;
        ContentRoot = _env.ContentRootPath;
        AssemblyVersion = typeof(DebugModel).Assembly.GetName().Version?.ToString() ?? "?";
        UtcNow = _timeProvider.GetUtcNow().UtcDateTime;
    }
}
