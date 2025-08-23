using ReceptRegister.Api.Endpoints;

namespace ReceptRegister.Api.Endpoints;

public static class ApiEndpointExtensions
{
	/// <summary>
	/// Maps all API endpoints (root redirect, health, recipes, taxonomy) in one fluent call.
	/// Keeps <c>Program.cs</c> minimal and enforces endpoint grouping conventions.
	/// </summary>
	public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("/", () => Results.Redirect("/health"));
		app.MapAppHealth();
		app.MapRecipeEndpoints();
		app.MapTaxonomyEndpoints();
		return app;
	}
}
