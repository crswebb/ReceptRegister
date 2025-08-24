namespace ReceptRegister.Api.Auth;

/// <summary>
/// Provides password strength scoring and suggestion generation. Server source of truth; client mirrors logic.
/// Score scale (0-6) based on length & character class diversity.
/// </summary>
public static class PasswordStrength
{
    public sealed record Result(int Score, string Label, IReadOnlyList<string> Suggestions)
    {
        public bool IsAcceptable => Score >= 3; // policy: at least 3/6 requirements satisfied
    }

    public static Result Evaluate(string password)
    {
        if (string.IsNullOrEmpty(password)) return new Result(0, "Empty", new List<string>{"Add a password"});
        int score = 0;
        var suggestions = new List<string>();
        if (password.Length >= 8) score++; else suggestions.Add("Use at least 8 characters");
        if (password.Length >= 12) score++; else suggestions.Add("Use 12+ characters");
        if (password.Any(char.IsLower)) score++; else suggestions.Add("Add a lowercase letter");
        if (password.Any(char.IsUpper)) score++; else suggestions.Add("Add an uppercase letter");
        if (password.Any(char.IsDigit)) score++; else suggestions.Add("Add a digit");
        if (password.Any(ch => !char.IsLetterOrDigit(ch))) score++; else suggestions.Add("Add a symbol");
        string label = score switch
        {
            <= 2 => "Weak",
            <= 4 => "Fair",
            5 => "Good",
            _ => "Strong"
        };
        // Trim suggestions to only ones still unmet, already captured
        return new Result(score, label, suggestions);
    }
}

/// <summary>
/// Logic extracted for deciding when to refresh a session to allow unit testing (issue #57).
/// </summary>
internal static class SessionRefreshDecider
{
    /// <summary>
    /// Returns true when remaining lifetime is below threshold portion of original TTL.
    /// </summary>
    public static bool ShouldRefresh(DateTimeOffset now, DateTimeOffset expiresAt, TimeSpan originalTtl, double thresholdPortion = 0.25)
    {
        if (originalTtl <= TimeSpan.Zero) return false;
        var remaining = expiresAt - now;
        if (remaining <= TimeSpan.Zero) return false; // already expired
        var portionLeft = remaining.TotalMilliseconds / originalTtl.TotalMilliseconds;
        return portionLeft <= thresholdPortion;
    }
}