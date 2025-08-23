using System.Security.Cryptography;

namespace ReceptRegister.Api.Auth;

public static class AuthSessionMiddlewareExtensions
{
	public static IApplicationBuilder UseAuthSession(this IApplicationBuilder app)
		=> app.UseMiddleware<AuthSessionMiddleware>();
}

internal sealed class AuthSessionMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ISessionService _sessions;
	private readonly SessionSettings _sessionSettings;

	public AuthSessionMiddleware(RequestDelegate next, ISessionService sessions, SessionSettings sessionSettings)
	{
		_next = next;
		_sessions = sessions;
		_sessionSettings = sessionSettings;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var path = context.Request.Path.Value ?? string.Empty;
		// Public / unauthenticated paths
		if (path == "/" || path.StartsWith("/health") || path.StartsWith("/auth"))
		{
			await _next(context);
			return;
		}

		if (context.Request.Cookies.TryGetValue(SessionCookie, out var token) && _sessions.Validate(token))
		{
			// For unsafe verbs require CSRF header (skip for /auth/* already handled above)
			if (IsUnsafe(context.Request.Method))
			{
				var csrfHeader = context.Request.Headers["X-CSRF-TOKEN"].FirstOrDefault();
				var expected = _sessions.GetCsrfToken(token);
				if (string.IsNullOrEmpty(csrfHeader) || expected is null ||
					!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(csrfHeader), System.Text.Encoding.UTF8.GetBytes(expected)))
				{
					context.Response.StatusCode = StatusCodes.Status403Forbidden;
					return;
				}
			}
			await _next(context);
			return;
		}

		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
	}

	private static bool IsUnsafe(string method) =>
		HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}