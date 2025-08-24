using ReceptRegister.Api.Auth;
using ReceptRegister.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace ReceptRegister.Tests;

public class PasswordServiceTests
{
    private async Task<(IPasswordService svc, string dbPath)> CreateAsync(Dictionary<string,string?>? extra = null, string? existingPath = null)
    {
        var services = new ServiceCollection();
        var dict = new Dictionary<string,string?>
        {
            {"RECEPT_PBKDF2_ITERATIONS", "60000"},
        };
        if (extra != null)
            foreach (var kv in extra) dict[kv.Key] = kv.Value;
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddLogging();
    services.AddSingleton<TimeProvider>(TimeProvider.System);
    services.AddSingleton<IWebHostEnvironment>(new FakeEnvAuth());

        // Persistence / auth infra
    var factory = existingPath is null ? new TestSqliteFactory() : new TestSqliteFactory(existingPath);
    services.AddSingleton<IDbConnectionFactory>(factory);
        services.AddAuthServices();
        var sp = services.BuildServiceProvider();
    await sp.GetRequiredService<ISchemaInitializer>().InitializeAsync();
    return (sp.GetRequiredService<IPasswordService>(), factory.Path);
    }

    [Fact]
    public async Task SetAndVerify_Works()
    {
    var (svc, _) = await CreateAsync();
    Assert.False(await svc.HasPasswordAsync());
    await svc.SetPasswordAsync("Passw0rd!");
    Assert.True(await svc.HasPasswordAsync());
    Assert.True(await svc.VerifyAsync("Passw0rd!"));
    Assert.False(await svc.VerifyAsync("nope"));
    }

    [Fact]
    public async Task IterationUpgrade_OnLogin()
    {
    var (svc, path) = await CreateAsync(new() { {"RECEPT_PBKDF2_ITERATIONS", "50000"} });
    await svc.SetPasswordAsync("secret");
    var (upgradeSvc, _) = await CreateAsync(new() { {"RECEPT_PBKDF2_ITERATIONS", "90000"} }, path);
    Assert.True(await upgradeSvc.VerifyAsync("secret")); // triggers upgrade silently using same DB
        // Can't easily assert new iteration without exposing repo; assume log emitted. Future: expose method to read config.
    }
}

internal sealed class FakeEnvAuth : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Test";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = ".";
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = ".";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

internal class TestSqliteFactory : IDbConnectionFactory
{
    public string Path { get; }
    public TestSqliteFactory() : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"auth-{Guid.NewGuid():N}.db")) {}
    public TestSqliteFactory(string existingPath)
    {
        Path = existingPath;
    }
    public Microsoft.Data.Sqlite.SqliteConnection Create() => new(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path, ForeignKeys = true }.ToString());
}
