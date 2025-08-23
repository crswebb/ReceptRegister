using Microsoft.Extensions.DependencyInjection;

namespace ReceptRegister.Api.Data;

public static class PersistenceServiceCollectionExtensions
{
	/// <summary>
	/// Registers persistence related services (SQLite connection factory and repositories).
	/// Keep this minimal; any future cross-cutting concerns (caching, metrics wrappers) get composed here.
	/// </summary>
	public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
	{
		services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
		services.AddScoped<IRecipesRepository, RecipesRepository>();
		services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();
		return services;
	}
}
