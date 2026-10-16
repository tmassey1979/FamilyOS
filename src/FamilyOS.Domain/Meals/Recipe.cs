using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

/// <summary>Reusable family recipe; ingredients feed meal plan and shopping.</summary>
public class Recipe : Entity
{
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public int Servings { get; private set; } = 4;
    public string? IngredientsCsv { get; private set; }
    public string? Instructions { get; private set; }

    private Recipe() { }

    public static Recipe Create(
        Guid familyId,
        string name,
        int servings = 4,
        string? ingredientsCsv = null,
        string? instructions = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipe name required.", nameof(name));
        if (servings < 1)
            throw new ArgumentException("Servings must be at least 1.", nameof(servings));

        return new Recipe
        {
            FamilyId = familyId,
            Name = name.Trim(),
            Servings = servings,
            IngredientsCsv = NormalizeCsv(ingredientsCsv),
            Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim()
        };
    }

    public void Update(string name, int servings, string? ingredientsCsv, string? instructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipe name required.", nameof(name));
        if (servings < 1)
            throw new ArgumentException("Servings must be at least 1.", nameof(servings));

        Name = name.Trim();
        Servings = servings;
        IngredientsCsv = NormalizeCsv(ingredientsCsv);
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        MarkUpdated();
    }

    public IReadOnlyList<string> IngredientList() =>
        string.IsNullOrWhiteSpace(IngredientsCsv)
            ? Array.Empty<string>()
            : IngredientsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string? NormalizeCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var joined = string.Join(", ", parts);
        return joined.Length == 0 ? null : joined;
    }
}
