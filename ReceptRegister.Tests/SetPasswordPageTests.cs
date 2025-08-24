using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using ReceptRegister.Frontend;
using Microsoft.AspNetCore.Builder;

namespace ReceptRegister.Tests;

public class SetPasswordPageTests
{
    [Fact]
    public async Task SetPassword_CreatesSession_AndRedirectsLoginIfAlreadySet()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        var frontendPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ReceptRegister.Frontend"));
    var tempRoot = Path.Combine(Path.GetTempPath(), "rr_frontendtests_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);
    builder.Environment.ContentRootPath = tempRoot;
        builder.Services.AddRazorPages(o => {
            o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        }).AddApplicationPart(typeof(ReceptRegister.Frontend.Pages.Recipes.IndexModel).Assembly);
        builder.Services.AddAppHealth();
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
    // Fresh DB under unique content root
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