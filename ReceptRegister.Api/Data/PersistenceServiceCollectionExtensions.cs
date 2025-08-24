namespace ReceptRegister.Api.Data;

public static class PersistenceServiceCollectionExtensions
{
	private const string ProviderConfigKey = "Database:Provider"; // expected values: SQLite | SqlServer
	private const string ConnectionStringKey = "Database:ConnectionString"; // used when Provider = SqlServer

	public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
	{
		services.AddSingleton<IDbConnectionFactory>(sp =>
		{
			var config = sp.GetRequiredService<IConfiguration>();
			var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PersistenceStartup");
			var env = sp.GetRequiredService<IWebHostEnvironment>();
			var provider = config[ProviderConfigKey];
			if (string.IsNullOrWhiteSpace(provider))
			{
				logger.LogInformation("No database provider configured (config key '{ProviderConfigKey}'); defaulting to SQLite.", ProviderConfigKey);
				return new SqliteConnectionFactory(config, env);
			}

			switch (provider.Trim())
			{
				case "SQLite":
					logger.LogInformation("Using SQLite database provider.");
					return new SqliteConnectionFactory(config, env);
				case "SqlServer":
					logger.LogInformation("Using SQL Server database provider.");
					return new SqlServerConnectionFactory(config);
				default:
					throw new InvalidOperationException($"Unsupported database provider '{provider}'. Expected one of: SQLite, SqlServer.");
			}
		});

		services.AddScoped<IRecipesRepository, RecipesRepository>();
		services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();
		return services;
	}
}
