using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Recipes;

public class EditModel : PageModel
{
    private readonly IRecipesRepository _recipes;
    public EditModel(IRecipesRepository recipes) => _recipes = recipes;

    [BindProperty]
    public RecipeInput? Input { get; set; }
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var recipe = await _recipes.GetByIdAsync(Id, ct);
        if (recipe is null) return Page();
        Input = new RecipeInput
        {
            Name = recipe.Name,
            Book = recipe.Book,
            Page = recipe.Page,
            Notes = recipe.Notes,
            Tried = recipe.Tried,
            Categories = string.Join(", ", recipe.Categories.Select(c => c.Name)),
            Keywords = string.Join(", ", recipe.Keywords.Select(k => k.Name))
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input is null) return Page();
        if (!Validate()) return Page();
        var existing = await _recipes.GetByIdAsync(Id, ct);
        if (existing is null) return Page();
        existing.Name = Input.Name!;
        existing.Book = Input.Book!;
        existing.Page = Input.Page;
        existing.Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes;
        existing.Tried = Input.Tried;
        var cats = Split(Input.Categories);
        var keys = Split(Input.Keywords);
        await _recipes.UpdateAsync(existing, cats, keys, ct);
        return RedirectToPage("/Recipes/Detail", new { id = existing.Id });
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Input!.Name)) ModelState.AddModelError(nameof(Input.Name), "Name is required");
        if (string.IsNullOrWhiteSpace(Input.Book)) ModelState.AddModelError(nameof(Input.Book), "Book is required");
        if (Input.Page < 1) ModelState.AddModelError(nameof(Input.Page), "Page must be >= 1");
        return ModelState.IsValid;
    }
    private static IEnumerable<string> Split(string? raw) => string.IsNullOrWhiteSpace(raw) ? Enumerable.Empty<string>() : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public class RecipeInput
    {
        public string? Name { get; set; }
        public string? Book { get; set; }
        public int Page { get; set; }
        public string? Notes { get; set; }
        public bool Tried { get; set; }
        public string? Categories { get; set; }
        public string? Keywords { get; set; }
    }
}