using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Recipes;

public class DetailModel : PageModel
{
    private readonly IRecipesRepository _recipes;
    public DetailModel(IRecipesRepository recipes) => _recipes = recipes;

    public Recipe? Recipe { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Recipe = await _recipes.GetByIdAsync(id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        await _recipes.DeleteAsync(id, ct);
        return RedirectToPage("/Recipes/Index");
    }
}