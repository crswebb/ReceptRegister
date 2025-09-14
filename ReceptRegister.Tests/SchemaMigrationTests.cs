using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Data.SchemaMigrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

public class SchemaMigrationTests
{
    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(cfg);
        services.AddSingleton<IWebHostEnvironment>(new FakeEnv());
        services.AddLogging();
        services.AddSingleton<DatabaseOptions>(_ => new DatabaseOptions { Provider = null }); // default SQLite
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<ISchemaInitializer, SqliteSchemaInitializer>();
        services.AddSingleton<ISchemaMigrator, SchemaMigrator>();
        services.AddSingleton<ILogger<SqliteSchemaInitializer>>(_ => NullLogger<SqliteSchemaInitializer>.Instance);
        services.AddSingleton<ILogger<SchemaMigrator>>(_ => NullLogger<SchemaMigrator>.Instance);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitialMigration_AppliesOnce()
    {
        var sp = BuildServices();
        var init = sp.GetRequiredService<ISchemaInitializer>();
        await init.InitializeAsync();
        var migrator = sp.GetRequiredService<ISchemaMigrator>();
        var result = await migrator.MigrateAsync();
        Assert.True(result.Applied >= 1); // initial schema (id=1)
        var result2 = await migrator.MigrateAsync();
        Assert.Equal(0, result2.Applied); // second run no new migrations
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
