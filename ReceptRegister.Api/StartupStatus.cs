namespace ReceptRegister.Api;

/// <summary>
/// Tracks one-time startup initialization (e.g. schema creation) so the health endpoint
/// can report a failure instead of the process crashing (causing platform 503s).
/// </summary>
public sealed class StartupStatus
{
    private Exception? _initException;
    public bool IsInitialized => InitializedAt is not null;
    public DateTimeOffset? InitializedAt { get; private set; }
    public bool Failed => _initException is not null;
<<<<<<< HEAD
    /// <summary>Short error (type + message) safe for basic health display.</summary>
    public string? Error => _initException is null ? null : _initException.GetType().Name + ": " + _initException.Message;
    /// <summary>Full exception.ToString() (stack trace etc). Only expose conditionally.</summary>
    public string? FullError => _initException?.ToString();
=======
    public string? Error => _initException?.GetType().Name + ": " + _initException?.Message;
>>>>>>> origin/main
    public void ReportSuccess()
    {
        InitializedAt = DateTimeOffset.UtcNow;
    }
    public void ReportFailure(Exception ex)
    {
        _initException = ex;
    }
}
