using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;

namespace ReceptRegister.Tests;

public class AuthRoundtripTests
{
    private async Task<(HttpClient client, IServiceProvider sp)> CreateAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAppHealth();
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
        app.MapApiEndpoints();
        await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());
        await app.StartAsync();
        return (app.GetTestClient(), app.Services);
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
        var setResp = await client.PostAsJsonAsync("/auth/set-password", new { Password = pwd });
        Assert.Equal(HttpStatusCode.NoContent, setResp.StatusCode);

        // Wrong login
        var badLogin = await client.PostAsJsonAsync("/auth/login", new { Password = "WrongPwd" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        // Correct login
        var goodLogin = await client.PostAsJsonAsync("/auth/login", new { Password = pwd });
        Assert.Equal(HttpStatusCode.OK, goodLogin.StatusCode);
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
