using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Recipes;

public class IndexModel : PageModel
{
    private readonly IRecipesRepository _repo;
    public IndexModel(IRecipesRepository repo) => _repo = repo;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public bool? Tried { get; set; }
    // Future: category/keyword multi-selects

    public PagedResult Result { get; private set; } = PagedResult.Empty;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (PageNumber < 1) PageNumber = 1;
        if (PageSize < 1) PageSize = 1; else if (PageSize > 100) PageSize = 100;
        var criteria = RecipeSearchCriteria.Create(Search, null, null, null, Tried, PageNumber, PageSize);
        var (items, total) = await _repo.SearchAsync(criteria, ct);
        Result = new PagedResult(items, criteria.Page, criteria.PageSize, total);
    }

    public record PagedResult(IReadOnlyList<Recipe> Items, int Page, int PageSize, int TotalItems)
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public static PagedResult Empty => new(Array.Empty<Recipe>(), 1, 20, 0);
    }
}
