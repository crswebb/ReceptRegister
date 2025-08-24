using ReceptRegister.Api.Data;
using ReceptRegister.Api.Domain;
using Microsoft.Data.Sqlite;

namespace ReceptRegister.Tests;

public class TempConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _cs;
    public TempConnectionFactory()
    {
        var file = Path.Combine(Path.GetTempPath(), $"recept-test-{Guid.NewGuid():N}.db");
        _cs = new SqliteConnectionStringBuilder { DataSource = file, ForeignKeys = true }.ToString();
    }
    public SqliteConnection Create() => new(_cs);
}

public class RecipeRepositoryTests
{
    private async Task<(IRecipesRepository recipes, ITaxonomyRepository taxonomy)> CreateReposAsync()
    {
        var factory = new TempConnectionFactory();
        await SchemaInitializer.InitializeAsync(factory);
        return (new RecipesRepository(factory), new TaxonomyRepository(factory));
    }

    [Fact]
    public async Task Add_Get_Update_Search_Delete_Flow()
    {
        var (repo, tax) = await CreateReposAsync();

        var recipe = new Recipe
        {
            Name = "Kanelbullar",
            Book = "Bröd och Bageri",
            Page = 123,
            Notes = "Classic Swedish buns",
            Tried = false
        };
        var id = await repo.AddAsync(recipe, new[] { "Buns", "Swedish" }, new[] { "cardamom", "yeast" });
        Assert.True(id > 0);

        var fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal(2, fetched!.Categories.Count);
        Assert.Equal(2, fetched.Keywords.Count);

        // Update
        fetched.Notes = "Add pearl sugar";
        fetched.Tried = true;
        await repo.UpdateAsync(fetched, new[] { "Buns" }, new[] { "cardamom", "sweet" });

        var updated = await repo.GetByIdAsync(id);
        Assert.NotNull(updated);
        Assert.Single(updated!.Categories);
        Assert.Equal(2, updated.Keywords.Count);
        Assert.True(updated.Tried);
        Assert.Contains("pearl", updated.Notes!);

    // Legacy search by keyword (string term)
    var legacySearch = await repo.LegacySearchAsync("sweet");
    Assert.Single(legacySearch);
    Assert.Equal(id, legacySearch[0].Id);

    // New paged search API: query sweet
    var criteria = RecipeSearchCriteria.Create("sweet", null, null, null, null, 1, 10);
    var (items, total) = await repo.SearchAsync(criteria);
    Assert.Equal(total, items.Count);
    Assert.Single(items);
    Assert.Equal(id, items[0].Id);

        // Toggle tried off
        await repo.ToggleTriedAsync(id, false);
        var toggled = await repo.GetByIdAsync(id);
        Assert.False(toggled!.Tried);

        // Delete
        await repo.DeleteAsync(id);
        var deleted = await repo.GetByIdAsync(id);
        Assert.Null(deleted);

        // Taxonomy lists contain categories / keywords (even after recipe deleted; we keep dictionary values)
        var categories = await tax.ListCategoriesAsync();
        var keywords = await tax.ListKeywordsAsync();
        Assert.Contains(categories, c => c.Name == "buns");
        Assert.Contains(keywords, k => k.Name == "sweet");
    }
}