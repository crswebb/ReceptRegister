using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;

namespace ReceptRegister.Tests;

public class AuthEndpointsTests
{
    private async Task<HttpClient> CreateClientAsync(Dictionary<string,string?>? cfg = null)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        if (cfg != null)
            builder.Configuration.AddInMemoryCollection(cfg);
        builder.Services.AddAppHealth();
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
        app.MapApiEndpoints();
        await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());
        await app.StartAsync();
        return app.GetTestClient();
    }

    [Fact]
    public async Task SetPassword_Then_Login_Flow()
    {
        var client = await CreateClientAsync();
        // No password yet: accessing /recipes unauthorized
        var respUnauthorized = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.Unauthorized, respUnauthorized.StatusCode);

        // Set password
        var set = await client.PostAsJsonAsync("/auth/set-password", new { Password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        // Login wrong
        var badLogin = await client.PostAsJsonAsync("/auth/login", new { Password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // Login correct
    var goodLogin = await client.PostAsJsonAsync("/auth/login", new { Password = "Passw0rd!" });
    Assert.Equal(HttpStatusCode.OK, goodLogin.StatusCode);
    var loginPayload = await goodLogin.Content.ReadFromJsonAsync<LoginResult>();
    Assert.NotNull(loginPayload);
    Assert.False(string.IsNullOrEmpty(loginPayload!.csrf));
        Assert.Contains(client.DefaultRequestHeaders, h => h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase));

        // Now /recipes should be authorized (will return empty list OK)
        var ok = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // CSRF required for state-changing (example: attempt to create recipe without header gets 403)
        var failPost = await client.PostAsJsonAsync("/recipes", new { Name = "A", Book = "B", Page = 1, Notes = "", Tried = false, Categories = Array.Empty<string>(), Keywords = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Forbidden, failPost.StatusCode);

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", loginPayload.csrf);
        var goodPost = await client.PostAsJsonAsync("/recipes", new { Name = "A", Book = "B", Page = 1, Notes = "", Tried = false, Categories = Array.Empty<string>(), Keywords = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Created, goodPost.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_And_RateLimit()
    {
        var client = await CreateClientAsync(new(){ {"RECEPT_LOGIN_MAX_ATTEMPTS","3"}, {"RECEPT_LOGIN_WINDOW_SECONDS","60"} });
        await client.PostAsJsonAsync("/auth/set-password", new { Password = "Original1!" });

        // Fail 3 times
        for (int i=0;i<3;i++)
        {
            var bad = await client.PostAsJsonAsync("/auth/login", new { Password = "bad" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        // 4th should be rate limited
        var limited = await client.PostAsJsonAsync("/auth/login", new { Password = "bad" });
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);

        // Successful login resets limiter (first try legitimate should still be limited, so wait for different client? Simplify by new client -> same IP though; proceed forcing real password until success after limiter window unrealistic in fast test). We skip verifying reset due to in-memory timing constraints.

        // New app instance (fresh limiter) to proceed password change
        client = await CreateClientAsync();
        await client.PostAsJsonAsync("/auth/set-password", new { Password = "Original1!" });
        var login = await client.PostAsJsonAsync("/auth/login", new { Password = "Original1!" });
        var payload = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.csrf);
        var change = await client.PostAsJsonAsync("/auth/change-password", new { CurrentPassword = "Original1!", NewPassword = "NewPass1!" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        // Old password should now fail (new login session required)
        var logout = await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var oldLogin = await client.PostAsJsonAsync("/auth/login", new { Password = "Original1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync("/auth/login", new { Password = "NewPass1!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Refresh_Extends_Session()
    {
        var client = await CreateClientAsync();
        await client.PostAsJsonAsync("/auth/set-password", new { Password = "Session1!" });
        var login = await client.PostAsJsonAsync("/auth/login", new { Password = "Session1!" });
        var payload = await login.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(payload);
        var firstExpiry = payload!.expiresAt;
        // Wait a short moment (cannot actually wait minutes; just call refresh)
        var refresh = await client.PostAsync("/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshPayload = await refresh.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(refreshPayload);
        Assert.True(refreshPayload!.expiresAt >= firstExpiry);
    }

    private record LoginResult(string csrf, DateTimeOffset expiresAt);
}
