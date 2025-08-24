using ReceptRegister.Api.Auth;
using Xunit;

namespace ReceptRegister.Tests;

public class PasswordStrengthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("short", 1)]
    [InlineData("lowercase12", 3)]
    [InlineData("Lower12!", 6)]
    public void Scores_Expected(string pwd, int minScore)
    {
        var r = PasswordStrength.Evaluate(pwd);
        Assert.True(r.Score >= minScore);
    }

    [Fact]
    public void Suggestions_For_Weak()
    {
        var r = PasswordStrength.Evaluate("abc");
        Assert.Contains(r.Suggestions, s => s.Contains("8"));
    }
}

public class SessionRefreshDeciderTests
{
    [Fact]
    public void Refresh_When_Quarter_Left()
    {
        var start = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(100);
        var exp = start + ttl * 0.20; // only 20% remains
        Assert.True(SessionRefreshDecider.ShouldRefresh(start, exp, ttl));
    }

    [Fact]
    public void No_Refresh_When_Plenty_Left()
    {
        var start = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(100);
        var exp = start + ttl * 0.80; // 80% remains
        Assert.False(SessionRefreshDecider.ShouldRefresh(start, exp, ttl));
    }
}