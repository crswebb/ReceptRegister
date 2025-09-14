using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ReceptRegister.Api.Data;
using System.Data;

namespace ReceptRegister.Api;

/// <summary>
/// Registers and maps diagnostic/health endpoints for the API host.
/// Endpoints provided:
///   GET /api/health         - Liveness + (quasi) readiness: status = starting|ok|error. Optional detailed stack trace when explicitly enabled.
///   GET /api/startup-error  - Plain text output of the startup exception (short or full) under same exposure rules.
///   GET /api/db-ping        - Lightweight connectivity probe + timing + minimal connection metadata.
///   GET /api/migrations     - Lists applied and pending schema migrations (best‑effort if history table present).
///
/// Security / Exposure:
///   Full exception detail is ONLY returned when one of these is true:
///     - ASPNETCORE_ENVIRONMENT == Development (env.IsDevelopment()).
///     - AppSetting: Diagnostics:ExposeHealthErrors = true.
///     - Environment variable EXPOSE_HEALTH_ERRORS=true.
///   Otherwise only a short "Type: Message" is emitted. This avoids leaking stack traces in production by default.
///
/// Operational Notes:
///   - /api/health intentionally avoids heavy dependencies (no DB IO when already failed/starting) to stay fast.
///   - /api/db-ping performs an actual open + simple SELECT to validate credentials/network.
///   - /api/migrations performs reflection to enumerate migration classes; if this becomes hot, consider caching.
///   - If startup initialization fails (schema etc.), the process keeps running so the platform can surface JSON error state instead of generic 503 pages.
///
/// Future Enhancements (optional):
///   - Add a reduced /api/ready endpoint that only returns ok when initialized & not failed.
///   - Redact sensitive substrings in FullError if certain providers embed secrets.
///   - Include build/version metadata (commit hash) in /api/health for traceability.
/// </summary>
public static class HealthExtensions
{
	public static IServiceCollection AddAppHealth(this IServiceCollection services)
	{
		services.AddHealthChecks()
			.AddCheck("self", () => HealthCheckResult.Healthy());
		// Track startup status for readiness style reporting.
		services.AddSingleton<StartupStatus>();
		return services;
	}

	public static IEndpointRouteBuilder MapAppHealth(this IEndpointRouteBuilder endpoints)
	{
		// Primary health endpoint (JSON). Chosen path /api/health to avoid collision with frontend root /health.
		endpoints.MapGet("/api/health", (HttpContext ctx, StartupStatus status, IConfiguration config, IWebHostEnvironment env) =>
		{
			bool detailRequested = ctx.Request.Query.ContainsKey("details") || ctx.Request.Query.ContainsKey("detail");
			bool allowDetails = env.IsDevelopment() || config.GetValue<bool>("Diagnostics:ExposeHealthErrors") || string.Equals(Environment.GetEnvironmentVariable("EXPOSE_HEALTH_ERRORS"), "true", StringComparison.OrdinalIgnoreCase);
			if (status.Failed)
			{
				return Results.Json(new
				{
					status = "error",
					app = "api",
					initialized = status.IsInitialized,
					error = status.Error,
					errorDetail = detailRequested && allowDetails ? status.FullError : null
				});
			}
			if (!status.IsInitialized)
			{
				return Results.Json(new { status = "starting", app = "api", initialized = false });
			}
			return Results.Json(new { status = "ok", app = "api", initialized = true });
		});

		// Plain text convenience endpoint for quick copy/paste of startup error (respects same exposure gating as /api/health?details).
		endpoints.MapGet("/api/startup-error", (StartupStatus status, IConfiguration config, IWebHostEnvironment env) =>
		{
			if (!status.Failed) return Results.Text("No startup error.");
			bool allowDetails = env.IsDevelopment() || config.GetValue<bool>("Diagnostics:ExposeHealthErrors") || string.Equals(Environment.GetEnvironmentVariable("EXPOSE_HEALTH_ERRORS"), "true", StringComparison.OrdinalIgnoreCase);
			if (!allowDetails) return Results.Text(status.Error ?? "Startup failed", "text/plain");
			return Results.Text(status.FullError ?? status.Error ?? "Startup failed", "text/plain");
		});

		// Lightweight DB connectivity probe: open + simple SELECT. Avoids schema enumerations; returns timing + provider + parsed server/database.
		endpoints.MapGet("/api/db-ping", async (IDbConnectionFactory factory, DatabaseOptions opts) =>
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				await using var conn = factory.Create();
				await conn.OpenAsync();
				await using (var cmd = conn.CreateCommand()) { cmd.CommandText = "SELECT 1"; await cmd.ExecuteScalarAsync(); }
				sw.Stop();
				string? server = null, database = null;
				try
				{
					var cs = conn.ConnectionString;
					foreach (var part in cs.Split(';', StringSplitOptions.RemoveEmptyEntries))
					{
						var kv = part.Split('=', 2);
						if (kv.Length != 2) continue;
						var key = kv[0].Trim().ToLowerInvariant();
						var val = kv[1].Trim();
						if (server is null && (key == "server" || key == "data source")) server = val;
						if (database is null && (key == "database" || key == "initial catalog")) database = val;
					}
				}
				catch { }
				return Results.Json(new { status = "ok", provider = opts.Provider ?? "SQLite", elapsedMs = sw.ElapsedMilliseconds, server, database });
			}
			catch (Exception ex)
			{
				sw.Stop();
				return Results.Json(new { status = "error", provider = opts.Provider ?? "SQLite", elapsedMs = sw.ElapsedMilliseconds, error = ex.GetType().Name + ": " + ex.Message });
			}
		});

		// Migrations endpoint: best-effort list of applied & pending migrations (reflection each call). Safe if history table missing.
		endpoints.MapGet("/api/migrations", async (IDbConnectionFactory factory, DatabaseOptions opts) =>
		{
			await using var conn = factory.Create();
			await conn.OpenAsync();
			var provider = opts.Provider ?? "SQLite";
			var applied = new List<object>();
			try
			{
				await using var cmd = conn.CreateCommand();
				cmd.CommandText = provider == "SqlServer" ? "SELECT Id, Name, AppliedAt FROM dbo.MigrationHistory ORDER BY Id" : "SELECT Id, Name, AppliedAt FROM MigrationHistory ORDER BY Id";
				await using var reader = await cmd.ExecuteReaderAsync();
				while (await reader.ReadAsync())
				{
					applied.Add(new { id = reader.GetInt32(0), name = reader.GetString(1), appliedAt = reader.GetValue(2) });
				}
			}
			catch { /* history table may not exist yet */ }
			var migrationTypes = typeof(ReceptRegister.Api.Data.SchemaMigrations.ISchemaMigration).Assembly
				.GetTypes()
				.Where(t => !t.IsAbstract && typeof(ReceptRegister.Api.Data.SchemaMigrations.ISchemaMigration).IsAssignableFrom(t))
				.Select(t => (ReceptRegister.Api.Data.SchemaMigrations.ISchemaMigration)Activator.CreateInstance(t)!)
				.OrderBy(m => m.Id)
				.ToList();
			var appliedIds = new HashSet<int>(applied.Select(a => (int)a.GetType().GetProperty("id")!.GetValue(a)!));
			var pending = migrationTypes.Where(m => !appliedIds.Contains(m.Id)).Select(m => new { id = m.Id, name = m.Name }).ToList();
			return Results.Json(new { applied, pending });
		});
		return endpoints;
	}
}
