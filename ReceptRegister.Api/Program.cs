using ReceptRegister.Api;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;
using ReceptRegister.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();
builder.Services.AddPersistenceServices();
builder.Services.AddAuthServices();

var app = builder.Build();

// Ensure database schema exists (tables created) before handling requests
await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());

app.UseAuthSession();
app.MapApiEndpoints();

app.Run();
