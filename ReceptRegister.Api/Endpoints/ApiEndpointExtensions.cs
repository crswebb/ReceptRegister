namespace ReceptRegister.Api.Endpoints;

public static class ApiEndpointExtensions
{
	public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapAppHealth();
		app.MapRecipeEndpoints();
		app.MapTaxonomyEndpoints();
		app.MapAuthEndpoints();
		return app;
	}
}
