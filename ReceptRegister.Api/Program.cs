using ReceptRegister.Api;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Endpoints;
using ReceptRegister.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();
builder.Services.AddPersistenceServices();
builder.Services.AddAuthServices();

var app = builder.Build();

app.UseAuthSession();
app.MapApiEndpoints();

app.Run();
