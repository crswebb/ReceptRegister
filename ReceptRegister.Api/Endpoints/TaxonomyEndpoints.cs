using ReceptRegister.Api.Data;

namespace ReceptRegister.Api.Endpoints;

public static class TaxonomyEndpoints
{
    public static IEndpointRouteBuilder MapTaxonomyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/categories", async (ITaxonomyRepository repo, CancellationToken ct) =>
        {
            var list = await repo.ListCategoriesAsync(ct);
            return Results.Ok(list.Select(c => c.Name));
        });

        app.MapGet("/keywords", async (ITaxonomyRepository repo, CancellationToken ct) =>
        {
            var list = await repo.ListKeywordsAsync(ct);
            return Results.Ok(list.Select(k => k.Name));
        });

        return app;
    }
}
