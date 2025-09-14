using ReceptRegister.Api;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data.Migration;

var migrateArg = args.FirstOrDefault(a => a.StartsWith("--migrate-sqlite=" , StringComparison.OrdinalIgnoreCase));
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();
builder.Services.AddPersistenceServices();
builder.Services.AddAuthServices();

var app = builder.Build();

// Migration mode: if --migrate-sqlite=path provided AND target provider is SqlServer, perform one-shot data migration then exit.
if (migrateArg is not null)
{
	var sourcePath = migrateArg.Split('=',2)[1].Trim('"');
	if (string.IsNullOrWhiteSpace(sourcePath))
	{
		Console.Error.WriteLine("Migration aborted: source SQLite path missing.");
		return; // do not start server
	}
	var dbOpts = app.Services.GetRequiredService<ReceptRegister.Api.Data.DatabaseOptions>();
	if (dbOpts.Provider is null || dbOpts.Provider == "SQLite")
	{
		Console.Error.WriteLine("Migration aborted: target provider must be SqlServer (configure Database:Provider=SqlServer)." );
		return;
	}
	await app.Services.GetRequiredService<ISchemaInitializer>().InitializeAsync();
	var runner = new ReceptRegister.Api.Data.Migration.DataMigrationRunner(app.Services.GetRequiredService<IDbConnectionFactory>(), app.Services.GetRequiredService<ILogger<ReceptRegister.Api.Data.Migration.DataMigrationRunner>>());
	var result = await runner.MigrateFromSqliteAsync(sourcePath, CancellationToken.None);
	if (!result.Success)
	{
		Console.Error.WriteLine($"Migration failed: {result.Message}");
		return;
	}
	Console.WriteLine($"Migration completed: {result.Message}");
	return; // exit without starting web host
}

// Ensure database schema exists (tables created) before handling requests (provider-specific)
// Capture any initialization failure instead of crashing the process so we can surface details via /api/health.
var startupStatus = app.Services.GetRequiredService<StartupStatus>();
try
{
	await app.Services.GetRequiredService<ISchemaInitializer>().InitializeAsync();
	startupStatus.ReportSuccess();
}
catch (Exception ex)
{
	startupStatus.ReportFailure(ex);
	// Continue booting: app will report error state via health endpoint.
}

app.UseAuthSession();
app.MapApiEndpoints();

app.Run();
