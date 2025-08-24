using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;
using ReceptRegister.Api;

namespace ReceptRegister.Tests;

public class AuthRoundtripTests
{
    private async Task<(HttpClient client, IServiceProvider sp)> CreateAsync()
    {
    var builder = WebApplication.CreateBuilder(Array.Empty<string>());
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rr_apitests_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);
    builder.Environment.ContentRootPath = tempRoot;
    // Environment forced via Host.UseEnvironment above
        // No special debug configuration required now
    // Use default logging configuration (no console provider in test project)
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
    public async Task Full_Roundtrip_Succeeds_And_Invalid_Fails()
    {
        var (client, sp) = await CreateAsync();
        const string pwd = "RoundTrip1!";
        // Password not set yet -> protected endpoint unauthorized
        var protectedResp = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResp.StatusCode);

        // Set password via endpoint
    var setResp = await client.PostAsync("/auth/set-password", new StringContent($"{{\"Password\":\"{pwd}\"}}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, setResp.StatusCode);
    var status = await client.GetAsync("/auth/status");
    var statusBody = await status.Content.ReadAsStringAsync();
    Assert.Contains("\"hasPassword\":true", statusBody);

        // Manual repository verification prior to login
        using (var scope = sp.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var cfg = await repo.GetAsync();
            Assert.NotNull(cfg);
            var manualOk = hasher.Verify(pwd, null, cfg!.Salt, cfg.Iterations, cfg.PasswordHash);
            if (!manualOk)
            {
                throw new Xunit.Sdk.XunitException($"Manual verify failed for password {pwd}. Iter={cfg.Iterations} Salt={Convert.ToBase64String(cfg.Salt)} Hash={Convert.ToBase64String(cfg.PasswordHash)}");
            }
        }

        // Wrong login
    var badLogin = await client.PostAsync("/auth/login", new StringContent("{\"Password\":\"WrongPwd\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // Correct login
    var goodLogin = await client.PostAsync("/auth/login", new StringContent($"{{\"Password\":\"{pwd}\"}}", Encoding.UTF8, "application/json"));
        if (goodLogin.StatusCode != HttpStatusCode.OK)
        {
            var dbg = await client.PostAsync("/auth/debug-verify", new StringContent($"{{\"Password\":\"{pwd}\"}}", Encoding.UTF8, "application/json"));
            var dbgBody = await dbg.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Login expected OK but was {goodLogin.StatusCode}. Debug: {dbg.StatusCode} {dbgBody}");
        }
        var loginPayload = await goodLogin.Content.ReadFromJsonAsync<LoginPayload>();
        Assert.NotNull(loginPayload);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload!.csrf));

        // Use CSRF for POST protected
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", loginPayload.csrf);
    var create = await client.PostAsJsonAsync("/recipes", new { Name = "R", Book = "B", Page = 1, Notes = "", Tried = false, Categories = Array.Empty<string>(), Keywords = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // GET protected now OK
        var list = await client.GetAsync("/recipes");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        // Negative: tamper with CSRF
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "deadbeef");
    var failTamper = await client.PostAsJsonAsync("/recipes", new { Name = "X", Book = "B", Page = 2, Notes = "", Tried = false, Categories = Array.Empty<string>(), Keywords = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Forbidden, failTamper.StatusCode);
    }

    private record LoginPayload(string csrf, DateTimeOffset expiresAt);
}
