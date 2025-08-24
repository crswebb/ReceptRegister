using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Auth; // reuse service from API project if referenced, else duplicate interface
using ReceptRegister.Api.Data;

namespace ReceptRegister.Frontend.Pages.Auth;

public class SetPasswordModel : PageModel
{
    private readonly IPasswordService _passwordService;
    private readonly ISessionService _sessions;
    private readonly SessionSettings _settings;
    private readonly IWebHostEnvironment _env;

    public SetPasswordModel(IPasswordService passwordService, ISessionService sessions, IWebHostEnvironment env, SessionSettings settings)
    {
        _passwordService = passwordService;
        _sessions = sessions;
        _env = env;
        _settings = settings;
    }

    [BindProperty]
    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool Success { get; private set; }
    public bool HasPasswordAlready { get; private set; }

    public async Task<IActionResult> OnGet([FromServices] IPasswordService svc)
    {
        HasPasswordAlready = await svc.HasPasswordAsync();
        if (HasPasswordAlready)
            return RedirectToPage("/Auth/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        HasPasswordAlready = await _passwordService.HasPasswordAsync();
        if (HasPasswordAlready)
        {
            // Avoid revealing state differences – just informational
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Server authoritative password strength evaluation (unified with client + tests)
        var strength = PasswordStrength.Evaluate(Password);
        if (!strength.IsAcceptable)
        {
            // Provide concise guidance; suggestions already ordered by unmet criteria
            ModelState.AddModelError(nameof(Password), "Password too weak: " + string.Join("; ", strength.Suggestions));
            return Page();
        }

        await _passwordService.SetPasswordAsync(Password);
        // Auto-login new admin
        var (token, csrf) = _sessions.CreateSession(false);
        SessionCookieWriter.Write(HttpContext, _settings, _env, _sessions, token, csrf);
        Success = true;
        ModelState.Clear();
        return Page();
    }
}
