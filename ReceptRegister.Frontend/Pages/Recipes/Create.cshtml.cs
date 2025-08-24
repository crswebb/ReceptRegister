using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Recipes;

public class CreateModel : PageModel
{
    private readonly IRecipesRepository _recipes;
    public CreateModel(IRecipesRepository recipes) => _recipes = recipes;

    [BindProperty]
    public RecipeInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Validate()) return Page();
        var recipe = new Recipe
        {
            Name = Input.Name!,
            Book = Input.Book!,
            Page = Input.Page,
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes,
            Tried = Input.Tried
        };
        var cats = Split(Input.Categories);
        var keys = Split(Input.Keywords);
        var id = await _recipes.AddAsync(recipe, cats, keys, ct);
        return RedirectToPage("/Recipes/Detail", new { id });
    }

    private static IEnumerable<string> Split(string? raw) => string.IsNullOrWhiteSpace(raw) ? Enumerable.Empty<string>() : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Input.Name)) ModelState.AddModelError(nameof(Input.Name), "Name is required");
        if (string.IsNullOrWhiteSpace(Input.Book)) ModelState.AddModelError(nameof(Input.Book), "Book is required");
        if (Input.Page < 1) ModelState.AddModelError(nameof(Input.Page), "Page must be >= 1");
        return ModelState.IsValid;
    }

    public class RecipeInput
    {
        public string? Name { get; set; }
        public string? Book { get; set; }
        public int Page { get; set; } = 1;
        public string? Notes { get; set; }
        public bool Tried { get; set; }
        public string? Categories { get; set; }
        public string? Keywords { get; set; }
    }
}