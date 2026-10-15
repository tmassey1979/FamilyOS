using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public class MealEntry : Entity
{
    public Guid MealPlanId { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTime DayUtc { get; private set; }
    public MealSlot Slot { get; private set; }
    public string DishName { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    /// <summary>Comma-separated ingredient hints for shopping list generation.</summary>
    public string? IngredientsCsv { get; private set; }

    private MealEntry() { }

    public static MealEntry Create(
        Guid mealPlanId,
        Guid familyId,
        DateTime dayUtc,
        MealSlot slot,
        string dishName,
        string? notes = null,
        string? ingredientsCsv = null)
    {
        return new MealEntry
        {
            MealPlanId = mealPlanId,
            FamilyId = familyId,
            DayUtc = dayUtc.Date,
            Slot = slot,
            DishName = dishName.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IngredientsCsv = NormalizeIngredients(ingredientsCsv)
        };
    }

    public void Update(string dishName, string? notes, string? ingredientsCsv)
    {
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name required.");
        DishName = dishName.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IngredientsCsv = NormalizeIngredients(ingredientsCsv);
        MarkUpdated();
    }

    public IReadOnlyList<string> IngredientList()
    {
        if (string.IsNullOrWhiteSpace(IngredientsCsv)) return Array.Empty<string>();
        return IngredientsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeIngredients(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
