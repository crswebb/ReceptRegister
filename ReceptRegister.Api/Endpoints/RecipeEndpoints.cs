using ReceptRegister.Api.Domain;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Api.Endpoints;

public static class RecipeEndpoints
{
	public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/recipes");

		// Enhanced search with paging & filters
		group.MapGet("/", async (
			string? query,
			string? book,
			int[]? categoryId,
			int[]? keywordId,
			bool? tried,
			int? page,
			int? pageSize,
			IRecipesRepository repo,
			CancellationToken ct) =>
		{
			var criteria = RecipeSearchCriteria.Create(query, book, categoryId, keywordId, tried, page, pageSize);
			var (items, total) = await repo.SearchAsync(criteria, ct);
			var dto = new PagedResult<RecipeSummaryDto>(items.Select(Mapping.ToSummary).ToList(), criteria.Page, criteria.PageSize, total);
			return Results.Ok(dto);
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

		group.MapPost("/{id:int}/tried", async (int id, RecipeTriedDto body, IRecipesRepository repo, CancellationToken ct) =>
		{
			var existing = await repo.GetByIdAsync(id, ct);
			if (existing is null) return Results.NotFound();
			await repo.ToggleTriedAsync(id, body.Tried, ct);
			var updated = await repo.GetByIdAsync(id, ct);
			return Results.Ok(new RecipeTriedDto(updated!.Id, updated.Tried));
		});

		// Attach / detach taxonomy by integer id (already existing taxonomy entries)
		group.MapPost("/{id:int}/categories/{categoryId:int}", async (int id, int categoryId, IRecipesRepository repo, CancellationToken ct) =>
		{
			var ok = await repo.AttachCategoryAsync(id, categoryId, ct);
			return ok ? Results.NoContent() : Results.NotFound();
		});
		group.MapDelete("/{id:int}/categories/{categoryId:int}", async (int id, int categoryId, IRecipesRepository repo, CancellationToken ct) =>
		{
			var ok = await repo.DetachCategoryAsync(id, categoryId, ct);
			return ok ? Results.NoContent() : Results.NotFound();
		});
		group.MapPost("/{id:int}/keywords/{keywordId:int}", async (int id, int keywordId, IRecipesRepository repo, CancellationToken ct) =>
		{
			var ok = await repo.AttachKeywordAsync(id, keywordId, ct);
			return ok ? Results.NoContent() : Results.NotFound();
		});
		group.MapDelete("/{id:int}/keywords/{keywordId:int}", async (int id, int keywordId, IRecipesRepository repo, CancellationToken ct) =>
		{
			var ok = await repo.DetachKeywordAsync(id, keywordId, ct);
			return ok ? Results.NoContent() : Results.NotFound();
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
