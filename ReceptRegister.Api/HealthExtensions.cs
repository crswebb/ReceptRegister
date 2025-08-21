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
		endpoints.MapGet("/health", () => Results.Ok(new { status = "ok", app = "api" }));
		return endpoints;
	}
}
