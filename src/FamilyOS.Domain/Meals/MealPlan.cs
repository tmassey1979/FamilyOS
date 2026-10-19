using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public class MealPlan : Entity
{
    public Guid FamilyId { get; private set; }

    private readonly List<MealEntry> _entries = new();
    public IReadOnlyCollection<MealEntry> Entries => _entries.AsReadOnly();

    private MealPlan() { }

    public static MealPlan Create(Guid familyId)
    {
        return new MealPlan { FamilyId = familyId };
    }

    public MealEntry UpsertMeal(DateTime dayUtc, MealSlot slot, string dishName, string? notes = null, string? ingredientsCsv = null)
    {
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name is required.", nameof(dishName));

        var day = dayUtc.Date;
        var existing = _entries.FirstOrDefault(e => e.DayUtc.Date == day && e.Slot == slot);

        if (existing is null)
        {
            var entry = MealEntry.Create(Id, FamilyId, day, slot, dishName.Trim(), notes, ingredientsCsv);
            _entries.Add(entry);
            MarkUpdated();
            return entry;
        }

        existing.Update(dishName.Trim(), notes, ingredientsCsv);
        MarkUpdated();
        return existing;
    }

    public void RemoveMeal(Guid entryId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) return;
        _entries.Remove(entry);
        MarkUpdated();
    }

    public IReadOnlyList<string> AllIngredients()
    {
        return _entries
            .SelectMany(e => e.IngredientList())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }
}
