namespace ReceptRegister.Api.Domain;

public record Category(int Id, string Name);
public record Keyword(int Id, string Name);

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Book { get; set; } = string.Empty;
    public int Page { get; set; }
    public string? Notes { get; set; }
    public bool Tried { get; set; }
    public List<Category> Categories { get; } = new();
    public List<Keyword> Keywords { get; } = new();
}