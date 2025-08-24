using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Recipes;

public class IndexModel : PageModel
{
    private readonly IRecipesRepository _repo;
    public IndexModel(IRecipesRepository repo) => _repo = repo;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public IReadOnlyList<Recipe> Results { get; private set; } = Array.Empty<Recipe>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Use legacy search path until Razor page gains paging/filter UI
        if (_repo is { } repo)
            Results = await repo.LegacySearchAsync(Search, ct);
    }
}
