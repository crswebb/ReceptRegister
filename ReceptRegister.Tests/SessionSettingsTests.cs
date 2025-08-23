using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Auth;
using Xunit;

namespace ReceptRegister.Tests;

public class SessionSettingsTests
{
    [Fact]
    public void Loads_From_Environment()
    {
        var dict = new Dictionary<string,string?>
        {
            {"RECEPT_SESSION_MINUTES","45"},
            {"RECEPT_SESSION_REMEMBER_MINUTES","43200"},
            {"RECEPT_SESSION_SAMESITE_STRICT","false"},
            {"RECEPT_SESSION_COOKIE","custom_session"},
            {"RECEPT_CSRF_COOKIE","custom_csrf"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var settings = SessionSettings.Load(config);
        Assert.Equal(45, settings.SessionMinutes);
        Assert.Equal(43200, settings.RememberMinutes);
        Assert.False(settings.SameSiteStrict);
        Assert.Equal("custom_session", settings.CookieName);
        Assert.Equal("custom_csrf", settings.CsrfCookieName);
    }

    [Fact]
    public void CookieWriter_Writes_Both_Cookies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SessionSettings>(SessionSettings.Load(new ConfigurationBuilder().Build()));
        services.AddSingleton<ISessionService, InMemorySessionService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IWebHostEnvironment>(new FakeEnv());
        var sp = services.BuildServiceProvider();
        var settings = sp.GetRequiredService<SessionSettings>();
        var sessions = sp.GetRequiredService<ISessionService>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var ctx = new DefaultHttpContext();
        var (token, csrf) = sessions.CreateSession();
        SessionCookieWriter.Write(ctx, settings, env, sessions, token, csrf);
        Assert.Contains(settings.CookieName, ctx.Response.Headers["Set-Cookie"].ToString());
        Assert.Contains(settings.CsrfCookieName, ctx.Response.Headers["Set-Cookie"].ToString());
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = ".";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = ".";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
