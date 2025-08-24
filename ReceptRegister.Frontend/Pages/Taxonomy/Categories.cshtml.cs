using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Taxonomy;

public class CategoriesModel : PageModel
{
    private readonly ITaxonomyRepository _taxonomy;
    private readonly IDbConnectionFactory _factory;
    public CategoriesModel(ITaxonomyRepository taxonomy, IDbConnectionFactory factory)
    { _taxonomy = taxonomy; _factory = factory; }

    public IReadOnlyList<Category> Categories { get; private set; } = Array.Empty<Category>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Categories = await _taxonomy.ListCategoriesAsync(ct);
    }

    public async Task<IActionResult> OnPostAddAsync(string name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var norm = name.Trim().ToLowerInvariant();
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Categories (Name) VALUES (@n) ON CONFLICT(Name) DO NOTHING"; // TODO provider-specific upsert strategy for SQL Server
            var p = cmd.CreateParameter();
            p.ParameterName = "@n";
            p.Value = norm;
            cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
    var cmd = conn.CreateCommand();
    cmd.CommandText = "DELETE FROM Categories WHERE Id=@id";
    var p = cmd.CreateParameter();
    p.ParameterName = "@id";
    p.Value = id;
    cmd.Parameters.Add(p);
    await cmd.ExecuteNonQueryAsync(ct);
        return RedirectToPage();
    }
}
