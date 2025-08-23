using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Auth;

namespace ReceptRegister.Frontend.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IPasswordService _passwordService;
    private readonly ISessionService _sessions;
    private readonly IWebHostEnvironment _env;
    private readonly SessionSettings _settings;

    public LoginModel(IPasswordService passwordService, ISessionService sessions, IWebHostEnvironment env, SessionSettings settings)
    {
        _passwordService = passwordService;
        _sessions = sessions;
        _env = env;
        _settings = settings;
    }

    [BindProperty]
    public string Password { get; set; } = string.Empty;
    [BindProperty]
    public bool RememberMe { get; set; }

    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGet([FromServices] IPasswordService svc)
    {
        // Redirect to set password if not set yet
        if (!await svc.HasPasswordAsync())
            return RedirectToPage("/Auth/SetPassword");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await _passwordService.HasPasswordAsync())
            return RedirectToPage("/Auth/SetPassword");
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Password required";
            return Page();
        }
        if (!await _passwordService.VerifyAsync(Password))
        {
            // Generic message
            ErrorMessage = "Login failed";
            return Page();
        }
    var (token, csrf) = _sessions.CreateSession(RememberMe);
    SessionCookieWriter.Write(HttpContext, _settings, _env, _sessions, token, csrf);
    Success = true;
    // Redirect to home after successful login
    return RedirectToPage("/Index");
    }
}