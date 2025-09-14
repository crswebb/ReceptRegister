using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Data;
using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ReceptRegister.Api.Data;
using System.Data;

namespace ReceptRegister.Api;

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
		// To avoid conflicts with frontend /health when unified, expose JSON health at /api/health.
		endpoints.MapGet("/api/health", (StartupStatus status) =>
		{
			if (status.Failed)
			{
				return Results.Json(new { status = "error", app = "api", initialized = status.IsInitialized, error = status.Error });
			}
			if (!status.IsInitialized)
			{
				return Results.Json(new { status = "starting", app = "api", initialized = false });
			}
			return Results.Json(new { status = "ok", app = "api", initialized = true });
		});


		// Lightweight DB connectivity probe: attempts open + SELECT 1. Returns basic timing and provider kind.
		endpoints.MapGet("/api/db-ping", async (IDbConnectionFactory factory, DatabaseOptions opts) =>
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				await using var conn = factory.Create();
				await conn.OpenAsync();
				await using (var cmd = conn.CreateCommand()) { cmd.CommandText = opts.Provider == "SqlServer" ? "SELECT 1" : "SELECT 1"; await cmd.ExecuteScalarAsync(); }
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

		// Migrations endpoint: lists applied + pending
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
