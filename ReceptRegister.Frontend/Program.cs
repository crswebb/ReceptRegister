using ReceptRegister.Frontend;
using ReceptRegister.Api.Data; // for AddPersistenceServices
using ReceptRegister.Api.Auth; // for AddAuthServices

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
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();
app.MapAppHealth();

app.Run();
