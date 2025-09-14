using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
			return Results.Json(new { status = "ok", app = "api", initialized = status.IsInitialized });
		});
		return endpoints;
	}
}
