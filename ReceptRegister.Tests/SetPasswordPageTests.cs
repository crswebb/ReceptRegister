using System.Net;
using Microsoft.AspNetCore.TestHost;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;

namespace ReceptRegister.Tests;

public class SetPasswordPageTests
{
    [Fact]
    public async Task SetPassword_CreatesSession_AndRedirectsLoginIfAlreadySet()
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
        var client = app.GetTestClient();

        // GET first time -> page
        var get = await client.GetAsync("/Auth/SetPassword");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // POST set password
        var form = new Dictionary<string,string> { {"Password","AdminPass1!"}, {"ConfirmPassword","AdminPass1!"} };
        var post = await client.PostAsync("/Auth/SetPassword", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        // Should have session cookie
        Assert.Contains(post.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) && h.Value.Any(v => v.StartsWith("rr_session")));

        // Second GET should redirect to login since password now set
        var get2 = await client.GetAsync("/Auth/SetPassword");
        Assert.Equal(HttpStatusCode.Redirect, get2.StatusCode);
        Assert.Equal("/Auth/Login", get2.Headers.Location?.ToString());
    }
}