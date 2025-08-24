using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Auth;

namespace ReceptRegister.Frontend.Pages.Auth;

public class LogoutModel : PageModel
{
    private readonly ISessionService _sessions;
    private readonly SessionSettings _settings;

    public LogoutModel(ISessionService sessions, SessionSettings settings)
    {
        _sessions = sessions;
        _settings = settings;
    }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        var cookieName = _settings.CookieName ?? "rr_session";
        if (Request.Cookies.TryGetValue(cookieName, out var tok))
        {
            _sessions.Invalidate(tok);
            Response.Cookies.Delete(cookieName);
        }
        return RedirectToPage("/Auth/Login");
    }
}
