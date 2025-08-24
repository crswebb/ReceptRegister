namespace ReceptRegister.Api.Data;

public static class PersistenceServiceCollectionExtensions
{
	public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
	{
		// Bind and validate database options once (singleton semantics OK here as config is static post-startup in typical hosting)
		services.AddSingleton(sp =>
		{
			var config = sp.GetRequiredService<IConfiguration>();
			var options = new DatabaseOptions();
			config.GetSection(DatabaseOptions.SectionName).Bind(options);
			options.Validate();
			return options;
		});

		services.AddSingleton<IDbConnectionFactory>(sp =>
		{
			var options = sp.GetRequiredService<DatabaseOptions>();
			var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PersistenceStartup");
			var env = sp.GetRequiredService<IWebHostEnvironment>();
			var provider = options.Provider;
			if (string.IsNullOrWhiteSpace(provider) || provider == "SQLite")
			{
				if (string.IsNullOrWhiteSpace(provider))
					logger.LogInformation("No database provider configured; defaulting to SQLite.");
				else
					logger.LogInformation("Using SQLite database provider.");
				return new SqliteConnectionFactory(sp.GetRequiredService<IConfiguration>(), env);
			}
			// provider already validated
			logger.LogInformation("Using SQL Server database provider.");
			return new SqlServerConnectionFactory(sp.GetRequiredService<IConfiguration>());
		});

		services.AddScoped<IRecipesRepository, RecipesRepository>();
		services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();
		return services;
	}
}
