using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReceptRegister.Api.Data;

/// <summary>
/// Runs schema initialization and automatic migrations at startup so deployment requires no manual migration step.
/// </summary>
public sealed class SchemaStartupHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SchemaStartupHostedService> _logger;
    private readonly StartupStatus _status;

    public SchemaStartupHostedService(IServiceProvider sp, ILogger<SchemaStartupHostedService> logger, StartupStatus status)
    {
        _sp = sp; _logger = logger; _status = status;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Schema startup hosted service beginning initialization + migrations...");
        var attempts = 0;
        var maxAttempts = 5;
        while (attempts < maxAttempts && !_status.Failed && !_status.IsInitialized)
        {
            attempts++;
            try
            {
                using var scope = _sp.CreateScope();
                var init = scope.ServiceProvider.GetRequiredService<ISchemaInitializer>();
                await init.InitializeAsync(cancellationToken);
                var migrator = scope.ServiceProvider.GetRequiredService<SchemaMigrations.ISchemaMigrator>();
                await migrator.MigrateAsync(cancellationToken);
                _status.ReportSuccess();
                _logger.LogInformation("Schema initialization + migrations complete (attempt {Attempt}).", attempts);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Schema auto-update attempt {Attempt} failed.", attempts);
                if (attempts >= maxAttempts)
                {
                    _status.ReportFailure(ex);
                }
                else
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts));
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
