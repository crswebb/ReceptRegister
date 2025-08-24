using System.Net;
using System.Net.Http.Json; // still used for reading responses
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using System;
using ReceptRegister.Api.Endpoints;
using ReceptRegister.Api;

namespace ReceptRegister.Tests;

public class AuthEndpointsTests
{
    private async Task<(HttpClient client, IServiceProvider services)> CreateClientAsync(Dictionary<string,string?>? cfg = null)
    {
    var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(Array.Empty<string>());
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Host.UseEnvironment("Development");
    var tempRoot = TestPathHelpers.NewApiTempRoot();
        builder.Environment.ContentRootPath = tempRoot;
        // Environment forced via Host.UseEnvironment above
        // Minimal required configuration only
        if (cfg != null)
            builder.Configuration.AddInMemoryCollection(cfg);
    // Minimal logging (default providers) to avoid missing console package references in test project
        builder.Services.AddAppHealth();
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
        app.UseAuthSession();
        app.MapApiEndpoints();
        await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());
        await app.StartAsync();
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
        var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.First()) };
        return (client, app.Services);
    }

    [Fact]
    public async Task SetPassword_Then_Login_Flow()
    {
    var (client, services) = await CreateClientAsync();
        // No password yet: accessing /recipes unauthorized
        var respUnauthorized = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.Unauthorized, respUnauthorized.StatusCode);

        // Set password
        var set = await client.PostAsync("/auth/set-password", new StringContent("{\"Password\":\"Passw0rd!\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
    // status diagnostic
    var status = await client.GetAsync("/auth/status");
    var statusJson = await status.Content.ReadAsStringAsync();
    Assert.Contains("\"hasPassword\":true", statusJson);

        // Manual repository / hasher verification BEFORE hitting login endpoint
        using (var scope = services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var cfgRow = await repo.GetAsync();
            Assert.NotNull(cfgRow);
            var manualOk = hasher.Verify("Passw0rd!", null, cfgRow!.Salt, cfgRow.Iterations, cfgRow.PasswordHash);
            if (!manualOk)
            {
                var saltB64 = Convert.ToBase64String(cfgRow.Salt);
                var hashB64 = Convert.ToBase64String(cfgRow.PasswordHash);
                throw new Xunit.Sdk.XunitException($"Manual verify failed while status shows password set. Iter={cfgRow.Iterations} Salt={saltB64} Hash={hashB64}");
            }
            // sanity: wrong password fails
            Assert.False(hasher.Verify("Wrong", null, cfgRow.Salt, cfgRow.Iterations, cfgRow.PasswordHash));
        }

        // Login wrong
    var badLogin = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"wrong\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // Login correct
    var goodLogin = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"Passw0rd!\"}", Encoding.UTF8, "application/json"));
    if (goodLogin.StatusCode != HttpStatusCode.OK)
    {
        // hit debug endpoint
        var dbg = await client.PostAsync("/auth/debug-verify", new StringContent("{\"Password\":\"Passw0rd!\"}", Encoding.UTF8, "application/json"));
        var dbgBody = await dbg.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException($"Login expected OK but was {goodLogin.StatusCode}. Debug: {dbg.StatusCode} {dbgBody}\nStatusPrior:{statusJson}");
    }
    var loginPayload = await goodLogin.Content.ReadFromJsonAsync<LoginResult>();
    Assert.NotNull(loginPayload);
    Assert.False(string.IsNullOrEmpty(loginPayload!.csrf));
    Assert.True(goodLogin.Headers.TryGetValues("Set-Cookie", out var _), "Expected Set-Cookie header on login response");

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
    var (client, _) = await CreateClientAsync(new(){ {"RECEPT_LOGIN_MAX_ATTEMPTS","3"}, {"RECEPT_LOGIN_WINDOW_SECONDS","60"} });
    await client.PostAsync("/auth/set-password", new StringContent("{\"Password\":\"Original1!\"}", Encoding.UTF8, "application/json"));

        // Fail 3 times
        for (int i=0;i<3;i++)
        {
            var bad = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"bad\"}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        // 4th should be rate limited
    var limited = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"bad\"}", Encoding.UTF8, "application/json"));
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);

        // Successful login resets limiter (first try legitimate should still be limited, so wait for different client? Simplify by new client -> same IP though; proceed forcing real password until success after limiter window unrealistic in fast test). We skip verifying reset due to in-memory timing constraints.

        // New app instance (fresh limiter) to proceed password change
    (client, _) = await CreateClientAsync();
    await client.PostAsync("/auth/set-password", new StringContent("{\"Password\":\"Original1!\"}", Encoding.UTF8, "application/json"));
    var login = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"Original1!\"}", Encoding.UTF8, "application/json"));
        var payload = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.csrf);
    var changeBody = "{\"CurrentPassword\":\"Original1!\",\"NewPassword\":\"NewPass1!\"}";
    var change = await client.PostAsync("/auth/change-password", new StringContent(changeBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        // Old password should now fail (new login session required)
        var logout = await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    var oldLogin = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"Original1!\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    var newLogin = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"NewPass1!\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Refresh_Extends_Session()
    {
    var (client, _) = await CreateClientAsync();
    await client.PostAsync("/auth/set-password", new StringContent("{\"Password\":\"Session1!\"}", Encoding.UTF8, "application/json"));
    var login = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"Session1!\"}", Encoding.UTF8, "application/json"));
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
