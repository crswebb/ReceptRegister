using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ReceptRegister.Frontend.Pages;

public class IndexRedirectModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Recipes/Index");
    }
}
