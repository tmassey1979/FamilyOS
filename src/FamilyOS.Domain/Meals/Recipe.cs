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

    /// <summary>
    /// Scale ingredient lines for a target serving count.
    /// Lines starting with a number are multiplied; others are left as-is with a note prefix when scaled.
    /// </summary>
    public IReadOnlyList<string> ScaleIngredientsForServings(int targetServings)
    {
        if (targetServings < 1)
            throw new ArgumentException("Target servings must be at least 1.", nameof(targetServings));

        var list = IngredientList();
        if (list.Count == 0 || targetServings == Servings)
            return list;

        var factor = (double)targetServings / Servings;
        var result = new List<string>(list.Count);
        foreach (var line in list)
        {
            result.Add(ScaleLine(line, factor));
        }
        return result;
    }

    private static string ScaleLine(string line, double factor)
    {
        var span = line.AsSpan().Trim();
        var i = 0;
        while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.'))
            i++;
        if (i == 0)
            return line;

        if (!double.TryParse(span[..i], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var qty))
            return line;

        var rest = span[i..].TrimStart().ToString();
        var scaled = qty * factor;
        var qtyStr = scaled is >= 10
            ? Math.Round(scaled, 0).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : Math.Round(scaled, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(rest) ? qtyStr : $"{qtyStr} {rest}";
    }

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
