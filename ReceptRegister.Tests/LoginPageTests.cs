using System.Net;
using Microsoft.AspNetCore.TestHost;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Tests;

public class LoginPageTests
{
    private async Task<HttpClient> CreateAsync()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.Services.AddRazorPages();
        builder.Services.AddAppHealth();
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
        app.MapRazorPages();
        await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());
        await app.StartAsync();
        return app.GetTestClient();
    }

    [Fact]
    public async Task Redirects_To_SetPassword_When_NoPassword()
    {
        var client = await CreateAsync();
        var resp = await client.GetAsync("/Auth/Login");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/Auth/SetPassword", resp.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_Sets_Extended_Cookie_When_RememberMe()
    {
        var client = await CreateAsync();
        // Set password first via page
        var formSet = new Dictionary<string,string>{{"Password","AdminPass1!"},{"ConfirmPassword","AdminPass1!"}};
        await client.PostAsync("/Auth/SetPassword", new FormUrlEncodedContent(formSet));

        var form = new Dictionary<string,string>{{"Password","AdminPass1!"},{"RememberMe","true"}};
        var login = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(form));
        // Redirect to home
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Index", login.Headers.Location?.ToString());
        // Session cookie present
        Assert.Contains(login.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) && h.Value.Any(v => v.StartsWith("rr_session")));
    }

    [Fact]
    public async Task Invalid_Login_Shows_Generic_Error()
    {
        var client = await CreateAsync();
        // create password
        var formSet = new Dictionary<string,string>{{"Password","AdminPass1!"},{"ConfirmPassword","AdminPass1!"}};
        await client.PostAsync("/Auth/SetPassword", new FormUrlEncodedContent(formSet));

        var form = new Dictionary<string,string>{{"Password","BadPass"}};
        var login = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(form));
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("Login failed", body);
    }
}