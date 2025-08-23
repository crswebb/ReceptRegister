namespace ReceptRegister.Api.Data;

public static class PersistenceServiceCollectionExtensions
{
	public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
	{
		services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
		services.AddScoped<IRecipesRepository, RecipesRepository>();
		services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();
		return services;
	}
}
