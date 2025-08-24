using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;

namespace ReceptRegister.Frontend.Pages.Taxonomy;

public class KeywordsModel : PageModel
{
    private readonly ITaxonomyRepository _taxonomy;
    private readonly ISqliteConnectionFactory _factory;
    public KeywordsModel(ITaxonomyRepository taxonomy, ISqliteConnectionFactory factory)
    { _taxonomy = taxonomy; _factory = factory; }

    public IReadOnlyList<Keyword> Keywords { get; private set; } = Array.Empty<Keyword>();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Keywords = await _taxonomy.ListKeywordsAsync(ct);
    }

    public async Task<IActionResult> OnPostAddAsync(string name, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var norm = name.Trim().ToLowerInvariant();
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Keywords (Name) VALUES ($n) ON CONFLICT(Name) DO NOTHING";
            cmd.Parameters.AddWithValue("$n", norm);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Keywords WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
        return RedirectToPage();
    }
}
