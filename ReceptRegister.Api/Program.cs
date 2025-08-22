using ReceptRegister.Api;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();

// Persistence services
builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddScoped<IRecipesRepository, RecipesRepository>();
builder.Services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();

var app = builder.Build();

// Ensure database schema exists
await using (var scope = app.Services.CreateAsyncScope())
{
	var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
	await SchemaInitializer.InitializeAsync(factory);
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapAppHealth();

var recipes = app.MapGroup("/recipes");

recipes.MapGet("/", async (string? search, IRecipesRepository repo, CancellationToken ct) =>
{
	var results = await repo.SearchAsync(search, ct);
	return Results.Ok(results.Select(Mapping.ToSummary));
});

recipes.MapGet("/{id:int}", async (int id, IRecipesRepository repo, CancellationToken ct) =>
{
	var recipe = await repo.GetByIdAsync(id, ct);
	return recipe is null ? Results.NotFound() : Results.Ok(Mapping.ToDetail(recipe));
});

recipes.MapPost("/", async (RecipeRequest req, IRecipesRepository repo, CancellationToken ct) =>
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

recipes.MapPut("/{id:int}", async (int id, RecipeRequest req, IRecipesRepository repo, CancellationToken ct) =>
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

recipes.MapPatch("/{id:int}/tried", async (int id, bool tried, IRecipesRepository repo, CancellationToken ct) =>
{
	var existing = await repo.GetByIdAsync(id, ct);
	if (existing is null) return Results.NotFound();
	await repo.ToggleTriedAsync(id, tried, ct);
	var updated = await repo.GetByIdAsync(id, ct);
	return Results.Ok(new { updated!.Id, updated.Tried });
});

recipes.MapDelete("/{id:int}", async (int id, IRecipesRepository repo, CancellationToken ct) =>
{
	var existing = await repo.GetByIdAsync(id, ct);
	if (existing is null) return Results.NotFound();
	await repo.DeleteAsync(id, ct);
	return Results.NoContent();
});

// Taxonomy endpoints
app.MapGet("/categories", async (ITaxonomyRepository repo, CancellationToken ct) =>
{
	var list = await repo.ListCategoriesAsync(ct);
	return Results.Ok(list.Select(c => c.Name));
});
app.MapGet("/keywords", async (ITaxonomyRepository repo, CancellationToken ct) =>
{
	var list = await repo.ListKeywordsAsync(ct);
	return Results.Ok(list.Select(k => k.Name));
});

app.Run();

// Make Program visible to test host
public partial class Program { }

// DTOs & mapping helpers (must come after top-level statements)
public record RecipeRequest(string Name, string Book, int Page, string? Notes, List<string> Categories, List<string> Keywords, bool Tried);
public record RecipeSummaryDto(int Id, string Name, string Book, int Page, bool Tried, string[] Categories, string[] Keywords);
public record RecipeDetailDto(int Id, string Name, string Book, int Page, string? Notes, bool Tried, string[] Categories, string[] Keywords);

public static class Mapping
{
	public static RecipeSummaryDto ToSummary(Recipe r) => new(
		r.Id, r.Name, r.Book, r.Page, r.Tried,
		r.Categories.Select(c => c.Name).ToArray(),
		r.Keywords.Select(k => k.Name).ToArray());

	public static RecipeDetailDto ToDetail(Recipe r) => new(
		r.Id, r.Name, r.Book, r.Page, r.Notes, r.Tried,
		r.Categories.Select(c => c.Name).ToArray(),
		r.Keywords.Select(k => k.Name).ToArray());
}
