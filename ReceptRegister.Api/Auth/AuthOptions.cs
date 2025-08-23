namespace ReceptRegister.Api.Auth;

public sealed class AuthOptions
{
	public string? Pepper { get; init; }
}

public static class AuthOptionsRegistration
{
	public static IServiceCollection AddAuthOptions(this IServiceCollection services, IConfiguration config, ILoggerFactory loggerFactory, IWebHostEnvironment env)
	{
		var pepper = config["RECEPT_PEPPER"];
		if (string.IsNullOrWhiteSpace(pepper))
		{
			var logger = loggerFactory.CreateLogger("AuthOptions");
			logger.LogWarning("No RECEPT_PEPPER configured. Define a strong secret pepper env var for improved password hash resilience{EnvNote}.", env.IsDevelopment() ? " (development okay)" : string.Empty);
		}
		services.AddSingleton(new AuthOptions { Pepper = string.IsNullOrWhiteSpace(pepper) ? null : pepper });
		return services;
	}

	// Convenience overload so callers don't need to manually resolve infra services
	public static IServiceCollection AddAuthOptions(this IServiceCollection services)
	{
		services.AddSingleton(sp =>
		{
			var config = sp.GetRequiredService<IConfiguration>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var env = sp.GetRequiredService<IWebHostEnvironment>();
			var pepper = config["RECEPT_PEPPER"];
			if (string.IsNullOrWhiteSpace(pepper))
			{
				var logger = loggerFactory.CreateLogger("AuthOptions");
				logger.LogWarning("No RECEPT_PEPPER configured. Define a strong secret pepper env var for improved password hash resilience{EnvNote}.", env.IsDevelopment() ? " (development okay)" : string.Empty);
			}
			return new AuthOptions { Pepper = string.IsNullOrWhiteSpace(pepper) ? null : pepper };
		});
		return services;
	}
}
