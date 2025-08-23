namespace ReceptRegister.Api.Auth;

public sealed class SessionSettings
{
    public string CookieName { get; init; } = "rr_session";
    public string CsrfCookieName { get; init; } = "rr_csrf";
    public bool SameSiteStrict { get; init; } = true; // allow override via env
    public int SessionMinutes { get; init; }
    public int RememberMinutes { get; init; }

    public static SessionSettings Load(IConfiguration config)
    {
        var sessionMinutes = config.GetValue("RECEPT_SESSION_MINUTES", 120);
        var rememberMinutes = config.GetValue("RECEPT_SESSION_REMEMBER_MINUTES", 60 * 24 * 30);
        var sameSite = config.GetValue("RECEPT_SESSION_SAMESITE_STRICT", true);
        var cookieName = config.GetValue("RECEPT_SESSION_COOKIE", "rr_session");
        var csrfCookie = config.GetValue("RECEPT_CSRF_COOKIE", "rr_csrf");
        return new SessionSettings { SessionMinutes = sessionMinutes, RememberMinutes = rememberMinutes, SameSiteStrict = sameSite, CookieName = cookieName, CsrfCookieName = csrfCookie };
    }
}

public static class SessionCookieWriter
{
    public static void Write(HttpContext ctx, SessionSettings settings, IWebHostEnvironment env, ISessionService sessions, string token, string csrf)
    {
        var expires = sessions.GetExpiry(token);
        var sameSite = settings.SameSiteStrict ? SameSiteMode.Strict : SameSiteMode.Lax;
        ctx.Response.Cookies.Append(settings.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = sameSite,
            Path = "/",
            Expires = expires
        });
        ctx.Response.Cookies.Append(settings.CsrfCookieName, csrf, new CookieOptions
        {
            HttpOnly = false,
            Secure = !env.IsDevelopment(),
            SameSite = sameSite,
            Path = "/",
            Expires = expires
        });
    }
}