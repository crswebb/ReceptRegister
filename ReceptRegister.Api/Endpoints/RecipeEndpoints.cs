using ReceptRegister.Api.Domain;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Api.Endpoints;

public static class RecipeEndpoints
{
	public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/recipes");

		group.MapGet("/", async (string? search, IRecipesRepository repo, CancellationToken ct) =>
		{
			var results = await repo.SearchAsync(search, ct);
			return Results.Ok(results.Select(Mapping.ToSummary));
		});

		group.MapGet("/{id:int}", async (int id, IRecipesRepository repo, CancellationToken ct) =>
		{
			var recipe = await repo.GetByIdAsync(id, ct);
			return recipe is null ? Results.NotFound() : Results.Ok(Mapping.ToDetail(recipe));
		});

		group.MapPost("/", async (RecipeRequest req, IRecipesRepository repo, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Book))
				return Results.ValidationProblem(new Dictionary<string, string[]>{{"Name/Book", new[]{"Name and Book are required"}}});
			var recipe = new Recipe
			{
				Name = req.Name.Trim(),
				Book = req.Book.Trim(),
				Page = req.Page,
				Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
				Tried = req.Tried
			};
			var id = await repo.AddAsync(recipe, req.Categories, req.Keywords, ct);
			return Results.Created($"/recipes/{id}", Mapping.ToDetail(recipe));
		});

		group.MapPut("/{id:int}", async (int id, RecipeRequest req, IRecipesRepository repo, CancellationToken ct) =>
		{
			var existing = await repo.GetByIdAsync(id, ct);
			if (existing is null) return Results.NotFound();
			existing.Name = req.Name.Trim();
			existing.Book = req.Book.Trim();
			existing.Page = req.Page;
			existing.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
			existing.Tried = req.Tried;
			await repo.UpdateAsync(existing, req.Categories, req.Keywords, ct);
			var updated = await repo.GetByIdAsync(id, ct);
			return Results.Ok(Mapping.ToDetail(updated!));
		});

		group.MapPatch("/{id:int}/tried", async (int id, bool tried, IRecipesRepository repo, CancellationToken ct) =>
		{
			var existing = await repo.GetByIdAsync(id, ct);
			if (existing is null) return Results.NotFound();
			await repo.ToggleTriedAsync(id, tried, ct);
			var updated = await repo.GetByIdAsync(id, ct);
			return Results.Ok(new { updated!.Id, updated.Tried });
		});

		group.MapDelete("/{id:int}", async (int id, IRecipesRepository repo, CancellationToken ct) =>
		{
			var existing = await repo.GetByIdAsync(id, ct);
			if (existing is null) return Results.NotFound();
			await repo.DeleteAsync(id, ct);
			return Results.NoContent();
		});

		return app;
	}
}
