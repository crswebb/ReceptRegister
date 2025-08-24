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
		return services;
	}

	public static IEndpointRouteBuilder MapAppHealth(this IEndpointRouteBuilder endpoints)
	{
		// To avoid conflicts with frontend /health when unified, expose JSON health at /api/health.
		endpoints.MapGet("/api/health", () => Results.Ok(new { status = "ok", app = "api" }));
		return endpoints;
	}
}
