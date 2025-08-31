using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using ReceptRegister.Api.Localization;

namespace ReceptRegister.Tests;

public class LocalizationEnvOverrideTests
{
    [Fact]
    public async Task EnvironmentVariables_Override_Configured_Cultures()
    {
        // Arrange
        var oldDef = Environment.GetEnvironmentVariable("RECEPT_DEFAULT_CULTURE");
        var oldSupp = Environment.GetEnvironmentVariable("RECEPT_SUPPORTED_CULTURES");
        try
        {
            Environment.SetEnvironmentVariable("RECEPT_DEFAULT_CULTURE", "sv-SE");
            Environment.SetEnvironmentVariable("RECEPT_SUPPORTED_CULTURES", "sv-SE,en-US");

            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddLocalization();
            builder.Services.AddConfiguredLocalization(builder.Configuration);
            var app = builder.Build();

            // Act
            var opts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;

            // Assert
            Assert.Equal("sv-SE", opts.DefaultRequestCulture.Culture.Name);
            Assert.Contains(opts.SupportedCultures, c => c.Name == "sv-SE");
            Assert.Contains(opts.SupportedCultures, c => c.Name == "en-US");
            Assert.Equal("sv-SE", CultureInfo.DefaultThreadCurrentCulture.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RECEPT_DEFAULT_CULTURE", oldDef);
            Environment.SetEnvironmentVariable("RECEPT_SUPPORTED_CULTURES", oldSupp);
        }
    }

    [Fact]
    public async Task InvalidCulture_FallsBack_To_enUS()
    {
        var oldDef = Environment.GetEnvironmentVariable("RECEPT_DEFAULT_CULTURE");
        try
        {
            Environment.SetEnvironmentVariable("RECEPT_DEFAULT_CULTURE", "xx-FAKE");
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());
            builder.Services.AddLocalization();
            builder.Services.AddConfiguredLocalization(builder.Configuration);
            var app = builder.Build();
            var opts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
            Assert.Equal("en-US", opts.DefaultRequestCulture.Culture.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RECEPT_DEFAULT_CULTURE", oldDef);
        }
    }
}
