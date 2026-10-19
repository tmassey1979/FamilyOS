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

    public MealEntry UpsertMeal(DateOnly day, string slot, string dishName, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(slot))
            throw new ArgumentException("Meal slot is required.");
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name is required.");

        var normalizedSlot = slot.Trim();
        var existing = _entries.FirstOrDefault(e =>
            e.Date == day && e.Slot.Equals(normalizedSlot, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            var entry = MealEntry.Create(FamilyId, day, normalizedSlot, dishName.Trim(), notes: notes);
            _entries.Add(entry);
            MarkUpdated();
            return entry;
        }

        existing.Update(title: dishName.Trim(), notes: notes);
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
}
