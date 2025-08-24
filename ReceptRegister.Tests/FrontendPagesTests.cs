using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Auth;
using ReceptRegister.Frontend;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;

namespace ReceptRegister.Tests;

public class FrontendPagesTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    private async Task<HttpClient> CreateAsync()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseTestServer();
        // Ensure Razor Pages from Frontend project are discoverable
        var frontendPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ReceptRegister.Frontend"));
        // Use a unique temp content root to isolate database per test while still loading Razor pages via application part
        var tempRoot = Path.Combine(Path.GetTempPath(), "rr_frontendtests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        _tempRoots.Add(tempRoot);
        builder.Environment.ContentRootPath = tempRoot;
        builder.Services.AddRazorPages(o => {
            o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
        }).AddApplicationPart(typeof(ReceptRegister.Frontend.Pages.Recipes.IndexModel).Assembly);
        builder.Services.AddPersistenceServices();
        builder.Services.AddAuthServices();
        builder.Services.AddAppHealth();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        var app = builder.Build();
        // Fresh database will be created under the unique content root
        app.MapRazorPages();
        await SchemaInitializer.InitializeAsync(app.Services.GetRequiredService<ISqliteConnectionFactory>());
        await app.StartAsync();
        return app.GetTestClient();
    }

    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    [Fact]
    public async Task Recipes_Index_Empty_State()
    {
        var client = await CreateAsync();
        var resp = await client.GetAsync("/Recipes/Index");
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("No recipes yet", html);
    }

    [Fact]
    public async Task Recipe_Create_Edit_Delete_Roundtrip()
    {
        var client = await CreateAsync();
        // Create
        // Priming GET (may set any required cookies)
        await client.GetAsync("/Recipes/Create");
        var createForm = new Dictionary<string,string>{
            {"Name","Test Recipe"},
            {"Book","Sample"},
            {"Page","10"},
            {"Categories","Dinner, Fast"},
            {"Keywords","quick, tasty"},
            {"Notes","Initial notes"}
        };
        var createResp = await client.PostAsync("/Recipes/Create", new FormUrlEncodedContent(createForm));
        if (createResp.StatusCode != HttpStatusCode.Redirect)
        {
            var body = await createResp.Content.ReadAsStringAsync();
            throw new Exception($"Create failed: {(int)createResp.StatusCode} {createResp.StatusCode}\n{body}");
        }
        var detailUrl = createResp.Headers.Location!.ToString();
        Assert.StartsWith("/Recipes/Detail/", detailUrl);

        // Get detail
        var detail = await client.GetAsync(detailUrl);
        var detailHtml = await detail.Content.ReadAsStringAsync();
        Assert.Contains("Test Recipe", detailHtml);
        Assert.Contains("dinner", detailHtml);
        Assert.Contains("quick", detailHtml);

        // Extract id
        var id = int.Parse(detailUrl.Split('/').Last());

        // Edit
        var editForm = new Dictionary<string,string>{
            {"Id", id.ToString()},
            {"Name","Test Recipe Updated"},
            {"Book","Sample"},
            {"Page","11"},
            {"Categories","Dinner"},
            {"Keywords","quick"},
            {"Notes","Updated notes"},
            {"Tried","true"}
        };
        var editResp = await client.PostAsync($"/Recipes/Edit/{id}", new FormUrlEncodedContent(editForm));
        Assert.Equal(HttpStatusCode.Redirect, editResp.StatusCode);
        var editDetailUrl = editResp.Headers.Location!.ToString();
        var afterEdit = await client.GetAsync(editDetailUrl);
        var afterEditHtml = await afterEdit.Content.ReadAsStringAsync();
        Assert.Contains("Updated notes", afterEditHtml);
        Assert.DoesNotContain("tasty", afterEditHtml); // removed keyword
        Assert.Contains("Yes", afterEditHtml); // Tried yes

        // Delete
        var deleteForm = new Dictionary<string,string>{{"Id", id.ToString()}};
        var deleteResp = await client.PostAsync($"/Recipes/Detail/{id}?handler=delete", new FormUrlEncodedContent(deleteForm));
        Assert.Equal(HttpStatusCode.Redirect, deleteResp.StatusCode);
        var redirectLocation = deleteResp.Headers.Location!.ToString();
        Assert.True(redirectLocation == "/Recipes/Index" || redirectLocation == "/Recipes", $"Unexpected delete redirect: {redirectLocation}");

        // Index no longer shows recipe name
        var index = await client.GetAsync("/Recipes/Index");
        var indexHtml = await index.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Test Recipe Updated", indexHtml);
    }

    [Fact]
    public async Task Recipe_Search_Fallback_Works()
    {
        var client = await CreateAsync();
        // create two recipes
        async Task Add(string name, string book, string cats, string keys)
        {
            var form = new Dictionary<string,string>{{"Name",name},{"Book",book},{"Page","1"},{"Categories",cats},{"Keywords",keys}};
            await client.PostAsync("/Recipes/Create", new FormUrlEncodedContent(form));
        }
        await Add("Apple Pie","Desserts","Dessert","apple,sweet");
        await Add("Veggie Pizza","Meals","Dinner","savory,quick");

        var search = await client.GetAsync("/Recipes/Index?Search=apple");
        var searchHtml = await search.Content.ReadAsStringAsync();
        Assert.Contains("Apple Pie", searchHtml);
        Assert.DoesNotContain("Veggie Pizza", searchHtml);

        // Category search
        var catSearch = await client.GetAsync("/Recipes/Index?Search=Dinner");
        var catHtml = await catSearch.Content.ReadAsStringAsync();
        Assert.Contains("Veggie Pizza", catHtml);
        Assert.DoesNotContain("Apple Pie", catHtml);
    }
}