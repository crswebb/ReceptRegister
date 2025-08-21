using ReceptRegister.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppHealth();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));
app.MapAppHealth();

app.Run();
