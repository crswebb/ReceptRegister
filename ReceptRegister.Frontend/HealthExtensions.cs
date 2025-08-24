using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ReceptRegister.Frontend;

// Frontend no longer defines a conflicting JSON health endpoint; instead it offers a simple text endpoint
// while API project extension (ReceptRegister.Api.HealthExtensions) can still map its JSON variant under /api/health if desired.
public static class HealthExtensions
{
	public static IServiceCollection AddAppHealth(this IServiceCollection services)
	{
		return services; // still no specific checks
	}

	public static IEndpointRouteBuilder MapAppHealth(this IEndpointRouteBuilder endpoints)
	{
		// In unified hosting mode we skip mapping /health to avoid conflicts; rely on /api/health.
		return endpoints;
	}
}
