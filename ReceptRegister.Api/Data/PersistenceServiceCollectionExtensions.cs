namespace ReceptRegister.Api.Data;

public static class PersistenceServiceCollectionExtensions
{
	public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
	{
		// Register schema initializer per provider AFTER DatabaseOptions is registered.
		// Bind and validate database options once (singleton semantics OK here as config is static post-startup in typical hosting)
		services.AddSingleton(sp =>
		{
			var config = sp.GetRequiredService<IConfiguration>();
			var options = new DatabaseOptions();
			config.GetSection(DatabaseOptions.SectionName).Bind(options);

			// Environment variable overrides (feature #144):
			// RECEPT_DB_PROVIDER=SQLite|SqlServer
			// RECEPT_DB_CONNECTIONSTRING="<connection string>"
			var envProvider = Environment.GetEnvironmentVariable("RECEPT_DB_PROVIDER");
			if (!string.IsNullOrWhiteSpace(envProvider))
			{
				options.Provider = envProvider.Trim();
			}
			var envConn = Environment.GetEnvironmentVariable("RECEPT_DB_CONNECTIONSTRING")
				?? Environment.GetEnvironmentVariable("RECEPT_DB_CONNECTION"); // allow alternate suffix
			if (!string.IsNullOrEmpty(envConn))
			{
				options.ConnectionString = envConn;
			}
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

		// Dialect
		services.AddSingleton<IDatabaseDialect>(sp =>
		{
			var options = sp.GetRequiredService<DatabaseOptions>();
			return (options.Provider is null || options.Provider == "SQLite")
				? new SqliteDialect()
				: new SqlServerDialect();
		});

		// Provider-specific schema initializer
		services.AddSingleton<ISchemaInitializer>(sp =>
		{
			var options = sp.GetRequiredService<DatabaseOptions>();
			return (options.Provider is null || options.Provider == "SQLite")
				? new SqliteSchemaInitializer(sp.GetRequiredService<IDbConnectionFactory>(), sp.GetRequiredService<ILogger<SqliteSchemaInitializer>>())
				: new SqlServerSchemaInitializer(sp.GetRequiredService<IDbConnectionFactory>(), sp.GetRequiredService<ILogger<SqlServerSchemaInitializer>>());
		});
		return services;
	}
}
