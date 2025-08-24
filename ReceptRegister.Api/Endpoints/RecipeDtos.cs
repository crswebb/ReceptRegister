using ReceptRegister.Api.Domain;

namespace ReceptRegister.Api.Endpoints;

public record RecipeRequest(string Name, string Book, int Page, string? Notes, List<string> Categories, List<string> Keywords, bool Tried);
public record RecipeSummaryDto(int Id, string Name, string Book, int Page, bool Tried, string[] Categories, string[] Keywords);
public record RecipeDetailDto(int Id, string Name, string Book, int Page, string? Notes, bool Tried, string[] Categories, string[] Keywords);

public record RecipeTriedDto(int Id, bool Tried);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
	public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}

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
