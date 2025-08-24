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
	private readonly IServiceScopeFactory _scopeFactory;
	private const string SessionCookie = "rr_session";

	public AuthSessionMiddleware(RequestDelegate next, ISessionService sessions, IServiceScopeFactory scopeFactory)
	{
		_next = next;
		_sessions = sessions;
		_scopeFactory = scopeFactory;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var path = context.Request.Path.Value ?? string.Empty;
		// Always allow basic health & auth endpoints to pass through (case-insensitive)
		if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
		{
			await _next(context);
			return;
		}

		// Setup mode handling (no password yet configured)
		await using (var scope = _scopeFactory.CreateAsyncScope())
		{
			var passwordSvc = scope.ServiceProvider.GetRequiredService<IPasswordService>();
			if (!await passwordSvc.HasPasswordAsync())
			{
				// Allow static asset requests so SetPassword page renders correctly
				if (IsStaticAsset(path))
				{
					await _next(context);
					return;
				}
				// If not already requesting set password page, redirect browsers there. For API calls return 503.
				if (!path.StartsWith("/Auth/SetPassword", StringComparison.OrdinalIgnoreCase))
				{
					var accept = context.Request.Headers["Accept"].ToString();
					if (accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) || !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
					{
						context.Response.Redirect("/Auth/SetPassword");
					}
					else
					{
						context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
						await context.Response.WriteAsync("Setup required (password not set).", System.Text.Encoding.UTF8);
					}
					return;
				}
				await _next(context); // already on SetPassword page
				return;
			}
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

		// Unauthenticated: redirect HTML requests to login, otherwise 401 JSON/plain
		var acceptHeader = context.Request.Headers["Accept"].ToString();
		if (acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase))
		{
			// Avoid redirect loop: if already on login page just return 401 (will show page if routed) or let pipeline handle if route exists
			if (!path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase))
			{
				context.Response.Redirect("/Auth/Login");
				return;
			}
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
	}

	private static bool IsStaticAsset(string path)
	{
		// crude allowlist; adjust as needed (case-insensitive)
		return path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsUnsafe(string method) =>
		HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}