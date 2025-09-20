using ReceptRegister.Api.Data; // for AddPersistenceServices
using ReceptRegister.Api.Auth; // for AddAuthServices + UseAuthSession
using ReceptRegister.Api.Endpoints; // for MapApiEndpoints
using ReceptRegister.Api.Localization;
using ReceptRegister.Api; // for AddConfiguredLocalization

var builder = WebApplication.CreateBuilder(args);
string bindUrl = "default";

// Force explicit binding so Azure (expects 8080) and the app align even if PORT was set incorrectly.
// If a PORT env var is present (e.g. for local overrides), honor it; otherwise default 8080.
var forcedPort = Environment.GetEnvironmentVariable("PORT");
bindUrl = "http://0.0.0.0:8080";
if (int.TryParse(forcedPort, out var parsed) && parsed > 0 && parsed < 65536)
{
    bindUrl = $"http://0.0.0.0:{parsed}";
}
builder.WebHost.UseUrls(bindUrl);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddLocalization(); // resource-based UI strings
builder.Services.AddConfiguredLocalization(builder.Configuration); // configure supported cultures
builder.Services.AddAppHealth();
// Reuse API auth/persistence services for password setup page
builder.Services.AddPersistenceServices();
builder.Services.AddAuthServices();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<StartupStatus>();

var app = builder.Build();

// Log chosen URLs early (shows up in stdout / container logs)
try
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogInformation("[Startup] Binding URLs: {Urls}", string.Join(',', app.Urls));
}
catch { /* non-fatal */ }

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Only redirect to HTTPS outside Development to avoid dev warning when no HTTPS endpoint is configured.
    app.UseHttpsRedirection();
}

app.UseRouting();

// Schema initialization + migrations handled by SchemaStartupHostedService (background) so health can show 'starting'.
// Auth session (cookie + csrf) before endpoints
app.UseAuthSession();

app.UseAuthorization();

// Serve static + UI
app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

// Expose API endpoints from the referenced API assembly so frontend & API share origin
app.MapApiEndpoints();

app.MapAppHealth();

// Log when the application has fully started and Kestrel has bound the ports.
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogInformation("[Startup] Application started. Listening on: {BindUrl}", bindUrl);
    }
    catch { /* non-fatal */ }
});

// IMPORTANT: Run the app so the process stays alive (was previously missing, causing container exit)
await app.RunAsync();
