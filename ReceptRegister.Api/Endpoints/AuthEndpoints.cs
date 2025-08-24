using Microsoft.AspNetCore.Http;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Api.Endpoints;

public static class AuthEndpoints
{
	private const string SessionCookieFallback = "rr_session"; // kept for backward compatibility if settings missing

	public record SetPasswordRequest(string Password);
	public record LoginRequest(string Password);
	public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

	public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/auth");

		group.MapGet("/status", async (IPasswordService svc, HttpContext ctx, ISessionService sessions, SessionSettings settings) =>
		{
			var hasPwd = await svc.HasPasswordAsync();
			var loggedIn = false;
			var cookieName = settings.CookieName ?? SessionCookieFallback;
			if (ctx.Request.Cookies.TryGetValue(cookieName, out var token))
				loggedIn = sessions.Validate(token);
			DateTimeOffset? exp = null; string? csrf = null;
			if (loggedIn && token is not null)
			{
				exp = sessions.GetExpiry(token);
				csrf = sessions.GetCsrfToken(token);
			}
			return Results.Ok(new { hasPassword = hasPwd, authenticated = loggedIn, expiresAt = exp, csrf });
		});

		group.MapPost("/set-password", async (SetPasswordRequest req, IPasswordService svc) =>
		{
			if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
				return Results.ValidationProblem(new Dictionary<string, string[]>{{"Password", new[]{"Minimum length 8"}}});
			var has = await svc.HasPasswordAsync();
			if (has) return Results.Conflict(new { message = "Password already set" });
			await svc.SetPasswordAsync(req.Password);
			return Results.NoContent();
		});

		group.MapPost("/login", async (LoginRequest req, IPasswordService svc, ISessionService sessions, HttpContext ctx, ILoginRateLimiter limiter, SessionSettings settings, IWebHostEnvironment env) =>
		{
			if (limiter.IsLimited(ctx))
				return Results.StatusCode(StatusCodes.Status429TooManyRequests);
			if (!await svc.HasPasswordAsync())
				return Results.Conflict(new { message = "Password not set yet" });
			if (string.IsNullOrEmpty(req.Password))
			{
				limiter.RecordFailure(ctx);
				return Results.Unauthorized();
			}
			if (!await svc.VerifyAsync(req.Password))
			{
				limiter.RecordFailure(ctx);
				return Results.Unauthorized();
			}
			var (token, csrf) = sessions.CreateSession();
			limiter.RecordSuccess(ctx);
			SessionCookieWriter.Write(ctx, settings, env, sessions, token, csrf);
			return Results.Ok(new { expiresAt = sessions.GetExpiry(token), csrf });
		});

		group.MapPost("/logout", (HttpContext ctx, ISessionService sessions, SessionSettings settings) =>
		{
			var cookieName = settings.CookieName ?? SessionCookieFallback;
			if (ctx.Request.Cookies.TryGetValue(cookieName, out var token))
				sessions.Invalidate(token);
			ctx.Response.Cookies.Delete(cookieName);
			return Results.NoContent();
		});

		group.MapPost("/change-password", async (ChangePasswordRequest req, IPasswordService svc) =>
		{
			if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
				return Results.ValidationProblem(new Dictionary<string,string[]>{{"NewPassword", new[]{"Minimum length 8"}}});
			if (!await svc.VerifyAsync(req.CurrentPassword))
				return Results.Unauthorized();
			await svc.SetPasswordAsync(req.NewPassword);
			return Results.NoContent();
		});

		group.MapPost("/refresh", (HttpContext ctx, ISessionService sessions, IWebHostEnvironment env, SessionSettings settings) =>
		{
			var cookieName = settings.CookieName ?? SessionCookieFallback;
			if (!ctx.Request.Cookies.TryGetValue(cookieName, out var token))
				return Results.Unauthorized();
			var (ok, newExpiry, csrf) = sessions.Refresh(token);
			if (!ok)
				return Results.Unauthorized();
			SessionCookieWriter.Write(ctx, settings, env, sessions, token, csrf!);
			return Results.Ok(new { expiresAt = newExpiry, csrf });
		});

		// Debug verification endpoint removed after test stabilization.

		return app;
	}
}