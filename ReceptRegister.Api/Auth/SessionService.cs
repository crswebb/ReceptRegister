using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace ReceptRegister.Api.Auth;

public interface ISessionService
{
	(string token, string csrf) CreateSession(bool extended = false);
	bool Validate(string token);
	DateTimeOffset? GetExpiry(string token);
	string? GetCsrfToken(string token);
	void Invalidate(string token);
	(bool success, DateTimeOffset? newExpiry, string? csrf) Refresh(string token);
	int TtlMinutes { get; }
}

internal sealed record SessionEntry(DateTimeOffset ExpiresAt, string CsrfToken);

public sealed class InMemorySessionService : ISessionService, IDisposable
{
	private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
	private readonly TimeSpan _ttl;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<InMemorySessionService> _logger;
	private readonly Timer _sweeper;
	private readonly TimeSpan _rememberTtl;
	private const int DefaultRememberMinutes = 60 * 24 * 30; // 30 days

	public InMemorySessionService(IConfiguration config, TimeProvider timeProvider, ILogger<InMemorySessionService> logger)
	{
		var minutes = config.GetValue("RECEPT_SESSION_MINUTES", 120);
		_ttl = TimeSpan.FromMinutes(minutes); // assign immediately after reading
		var rememberMinutes = config.GetValue("RECEPT_SESSION_REMEMBER_MINUTES", DefaultRememberMinutes); // default 30 days
		_ttl = TimeSpan.FromMinutes(minutes);
		_rememberTtl = TimeSpan.FromMinutes(rememberMinutes);
		_timeProvider = timeProvider;
		_logger = logger;
		_sweeper = new Timer(Sweep, null, _ttl, _ttl);
	}

	public int TtlMinutes => (int)_ttl.TotalMinutes;

	public (string token, string csrf) CreateSession(bool extended = false)
	{
		var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=');
		var csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
		var chosenTtl = extended ? _rememberTtl : _ttl;
		var expires = _timeProvider.GetUtcNow() + chosenTtl;
		_sessions[token] = new SessionEntry(expires, csrf);
		_logger.LogDebug("Created {Kind} session expiring at {Expiry}", extended ? "extended" : "standard", expires);
		return (token, csrf);
	}

	public bool Validate(string token)
	{
		if (_sessions.TryGetValue(token, out var entry))
		{
			if (entry.ExpiresAt > _timeProvider.GetUtcNow())
				return true;
			if (_sessions.TryRemove(token, out var removedEntry)) { /* removed expired session */ }
		}
		return false;
	}

	public DateTimeOffset? GetExpiry(string token) => _sessions.TryGetValue(token, out var e) ? e.ExpiresAt : null;

	public string? GetCsrfToken(string token) => _sessions.TryGetValue(token, out var e) ? e.CsrfToken : null;

	public (bool success, DateTimeOffset? newExpiry, string? csrf) Refresh(string token)
	{
		if (_sessions.TryGetValue(token, out var existing))
		{
			if (existing.ExpiresAt <= _timeProvider.GetUtcNow())
			{
				if (_sessions.TryRemove(token, out var expired)) { /* removed expired session */ }
				return (false, null, null);
			}
			var newExpiry = _timeProvider.GetUtcNow() + _ttl;
			_sessions[token] = existing with { ExpiresAt = newExpiry };
			return (true, newExpiry, existing.CsrfToken);
		}
		return (false, null, null);
	}

	public void Invalidate(string token)
	{
		if (_sessions.TryRemove(token, out var removed)) { /* invalidated */ }
	}

	private void Sweep(object? _) {
		var now = _timeProvider.GetUtcNow();
		var removed = 0;
		foreach (var kvp in _sessions)
		{
			if (kvp.Value.ExpiresAt <= now)
			{
				if (_sessions.TryRemove(kvp.Key, out var removedSession)) removed++;
			}
		}
		if (removed > 0)
			_logger.LogDebug("Swept {Count} expired sessions", removed);
	}

	public void Dispose() => _sweeper.Dispose();
}