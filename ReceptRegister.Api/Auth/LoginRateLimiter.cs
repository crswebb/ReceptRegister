using System.Collections.Concurrent;

namespace ReceptRegister.Api.Auth;

public interface ILoginRateLimiter
{
	bool IsLimited(HttpContext context);
	void RecordFailure(HttpContext context);
	void RecordSuccess(HttpContext context);
}

internal sealed class InMemoryLoginRateLimiter : ILoginRateLimiter
{
	private readonly ConcurrentDictionary<string,(int attempts, DateTimeOffset windowStart)> _state = new();
	private readonly int _maxAttempts;
	private readonly TimeSpan _window;
	private readonly TimeProvider _time;

	public InMemoryLoginRateLimiter(IConfiguration config, TimeProvider timeProvider)
	{
		_maxAttempts = config.GetValue("RECEPT_LOGIN_MAX_ATTEMPTS", 5);
		var windowSeconds = config.GetValue("RECEPT_LOGIN_WINDOW_SECONDS", 300);
		_window = TimeSpan.FromSeconds(windowSeconds);
		_time = timeProvider;
	}

	private string Key(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

	public bool IsLimited(HttpContext context)
	{
		var key = Key(context);
		if (_state.TryGetValue(key, out var entry))
		{
			var now = _time.GetUtcNow();
			if (now - entry.windowStart > _window)
			{
				_state.TryUpdate(key,(0, now), entry);
				return false;
			}
			return entry.attempts >= _maxAttempts;
		}
		return false;
	}

	public void RecordFailure(HttpContext context)
	{
		var key = Key(context);
		_state.AddOrUpdate(key,
			_ => (1, _time.GetUtcNow()),
			(_, existing) =>
			{
				var now = _time.GetUtcNow();
				if (now - existing.windowStart > _window)
					return (1, now);
				return (existing.attempts + 1, existing.windowStart);
			});
	}

	public void RecordSuccess(HttpContext context)
	{
		var key = Key(context);
		_state.TryRemove(key, out _);
	}
}