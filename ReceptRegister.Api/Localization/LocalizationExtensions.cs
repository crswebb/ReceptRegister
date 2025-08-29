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
        var defaultCultureName = section["DefaultCulture"] ?? "en-US"; // fallback if not configured
        var supported = section.GetSection("SupportedCultures").Get<string[]>() ?? new[] { defaultCultureName };

        // Ensure default exists in supported
        if (!supported.Contains(defaultCultureName, StringComparer.OrdinalIgnoreCase))
        {
            supported = supported.Concat(new[] { defaultCultureName }).ToArray();
        }

        var defaultCulture = new CultureInfo(defaultCultureName);
        var supportedCultures = supported.Select(c => new CultureInfo(c)).ToList();

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
}
