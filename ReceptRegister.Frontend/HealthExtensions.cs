using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ReceptRegister.Frontend;

public static class HealthExtensions
{
	public static IServiceCollection AddAppHealth(this IServiceCollection services)
	{
		return services; // front-end has no dependencies yet
	}

	public static IEndpointRouteBuilder MapAppHealth(this IEndpointRouteBuilder endpoints)
	{
		// Milestone 1 requirement: plain text health response (Issue #8)
		endpoints.MapGet("/health", () => Results.Text("ok", "text/plain"));
		return endpoints;
	}
}
