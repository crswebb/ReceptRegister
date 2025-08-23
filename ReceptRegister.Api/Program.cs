using ReceptRegister.Api;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;
using ReceptRegister.Api.Endpoints;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();

// Persistence services
builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddScoped<IRecipesRepository, RecipesRepository>();
builder.Services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();

var app = builder.Build();

// Ensure database schema exists
await using (var scope = app.Services.CreateAsyncScope())
{
	var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
	await SchemaInitializer.InitializeAsync(factory);
}

app.MapGet("/", () => Results.Redirect("/health"));
app.MapAppHealth();
app.MapRecipeEndpoints();
app.MapTaxonomyEndpoints();

app.Run();

// Make Program visible to test host
public partial class Program { }

// (Endpoint DTOs & mapping moved to Endpoints layer for separation of concerns)
