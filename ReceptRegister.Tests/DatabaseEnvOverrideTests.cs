using ReceptRegister.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ReceptRegister.Tests;

public class DatabaseEnvOverrideTests
{
    [Fact]
    public void EnvProvider_Overrides_Config()
    {
        Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", null);
        Environment.SetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING", null);
        var oldProv = Environment.GetEnvironmentVariable("RECEPT_DB_PROVIDER");
        try
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", "sqlite"); // lower-case variant
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddPersistenceServices();
            var app = builder.Build();
            var opts = app.Services.GetRequiredService<DatabaseOptions>();
            Assert.Equal("SQLite", opts.Provider); // normalized
        }
        finally
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", oldProv);
        }
    }

    [Fact]
    public void EnvConnectionString_Overrides_Config()
    {
        Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", null);
        Environment.SetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING", null);
        var oldProv = Environment.GetEnvironmentVariable("RECEPT_DB_PROVIDER");
        var oldConn = Environment.GetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING");
        try
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", "SqlServer");
            Environment.SetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING", "Server=.;Database=RR;Trusted_Connection=True;Encrypt=False");
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddPersistenceServices();
            var app = builder.Build();
            var opts = app.Services.GetRequiredService<DatabaseOptions>();
            Assert.Equal("SqlServer", opts.Provider);
            Assert.Contains("Database=RR", opts.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", oldProv);
            Environment.SetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING", oldConn);
        }
    }

    [Fact]
    public void MissingConnectionString_WhenSqlServer_Throws()
    {
        Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", null);
        Environment.SetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING", null);
        var oldProv = Environment.GetEnvironmentVariable("RECEPT_DB_PROVIDER");
        try
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", "SqlServer");
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            // Expect failure when building services because connection string absent
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.Services.AddPersistenceServices();
                var app = builder.Build();
                _ = app.Services.GetRequiredService<DatabaseOptions>();
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("RECEPT_DB_PROVIDER", oldProv);
        }
    }
}
