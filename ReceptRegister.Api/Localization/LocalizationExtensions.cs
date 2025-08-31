using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ReceptRegister.Api.Localization;

public static class LocalizationExtensions
{
    private const string SectionName = "Localization";

    /// <summary>
    /// Registers ASP.NET Core localization with cultures driven purely by configuration (no user switcher).
    /// Configuration shape:
    /// "Localization": {
    ///   "DefaultCulture": "en-US",
    ///   "SupportedCultures": [ "en-US", "sv-SE" ]
    /// }
    /// If SupportedCultures missing, default is used as the single supported culture.
    /// </summary>
    public static IServiceCollection AddConfiguredLocalization(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(SectionName);

        // Environment variable overrides (feature #137):
        // RECEPT_DEFAULT_CULTURE=sv-SE
        // RECEPT_SUPPORTED_CULTURES=sv-SE,en-US (comma / semicolon separated)
        var envDefault = Environment.GetEnvironmentVariable("RECEPT_DEFAULT_CULTURE");
        var envSupportedRaw = Environment.GetEnvironmentVariable("RECEPT_SUPPORTED_CULTURES");

        var defaultCultureName = (envDefault ?? section["DefaultCulture"]) ?? "en-US"; // fallback if not configured

        string[] supported;
        if (!string.IsNullOrWhiteSpace(envSupportedRaw))
        {
            var split = envSupportedRaw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => TrimWrappingQuotes(s))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            supported = split.Length > 0 ? split : new[] { defaultCultureName };
        }
        else
        {
            supported = section.GetSection("SupportedCultures").Get<string[]>() ?? new[] { defaultCultureName };
        }

        // Ensure default exists in supported
        if (!supported.Contains(defaultCultureName, StringComparer.OrdinalIgnoreCase))
        {
            supported = supported.Concat(new[] { defaultCultureName }).ToArray();
        }

        CultureInfo defaultCulture;
        try
        {
            defaultCulture = new CultureInfo(defaultCultureName);
        }
        catch (CultureNotFoundException)
        {
            // Fallback gracefully if an invalid culture code is supplied via env/config.
            defaultCulture = new CultureInfo("en-US");
            defaultCultureName = defaultCulture.Name; // normalize
        }

        var supportedCultures = new List<CultureInfo>();
        foreach (var c in supported)
        {
            try
            {
                supportedCultures.Add(new CultureInfo(c));
            }
            catch (CultureNotFoundException)
            {
                // Skip invalid supported culture entries silently; could add logging later.
            }
        }
        if (supportedCultures.Count == 0)
        {
            supportedCultures.Add(defaultCulture);
        }

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(defaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            // We are intentionally NOT adding UI-based providers (querystring/cookie) for now.
            options.RequestCultureProviders = new List<IRequestCultureProvider>
            {
                // Minimal fixed provider without relying on specific concrete class (ensures no extra package requirement)
                new FixedOnlyRequestCultureProvider(defaultCulture)
            };
        });

        // Set thread defaults so non-localization-aware code (e.g. DateTime.ToString()) respects culture.
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

        return services;
    }

    private sealed class FixedOnlyRequestCultureProvider : IRequestCultureProvider
    {
        private readonly RequestCulture _culture;
        public FixedOnlyRequestCultureProvider(CultureInfo culture) => _culture = new RequestCulture(culture);
        public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
            => Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(_culture.Culture.Name, _culture.UICulture.Name));
    }

    private static string TrimWrappingQuotes(string input)
    {
        if (input.Length >= 2)
        {
            if ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\''))
            {
                return input.Substring(1, input.Length - 2).Trim();
            }
        }
        return input;
    }
}
