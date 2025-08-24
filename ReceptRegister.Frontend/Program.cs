using ReceptRegister.Frontend;
using ReceptRegister.Api.Data; // for AddPersistenceServices
using ReceptRegister.Api.Auth; // for AddAuthServices + UseAuthSession
using ReceptRegister.Api.Endpoints; // for MapApiEndpoints

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddAppHealth();
// Reuse API auth/persistence services for password setup page
builder.Services.AddPersistenceServices();
builder.Services.AddAuthServices();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

var app = builder.Build();

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

app.Run();
