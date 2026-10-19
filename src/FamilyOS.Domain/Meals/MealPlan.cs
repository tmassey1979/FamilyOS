using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

public class MealPlan : Entity
{
    public Guid FamilyId { get; private set; }
    public DateTime WeekStartUtc { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<MealEntry> _entries = new();
    public IReadOnlyCollection<MealEntry> Entries => _entries.AsReadOnly();

    private MealPlan() { }

    /// <summary>Normalize any date to the Monday 00:00 UTC of that ISO-style week (Mon–Sun).</summary>
    public static DateTime ToMonday(DateTime anyUtc)
    {
        var d = DateTime.SpecifyKind(anyUtc.Date, DateTimeKind.Utc);
        // DayOfWeek: Sunday=0 … Saturday=6. Convert so Monday=0 … Sunday=6.
        var daysFromMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysFromMonday);
    }

    public static MealPlan Create(Guid familyId, DateTime weekStartUtc, string? notes = null)
    {
        return new MealPlan
        {
            FamilyId = familyId,
            WeekStartUtc = ToMonday(weekStartUtc),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public MealEntry UpsertEntry(
        DateTime dayUtc,
        MealSlot slot,
        string dishName,
        string? notes = null,
        string? ingredientsCsv = null)
    {
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name is required.", nameof(dishName));

        var day = dayUtc.Date;
        var existing = _entries.FirstOrDefault(e =>
            !e.IsDeleted && e.DayUtc.Date == day && e.Slot == slot);

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

    public void RemoveEntry(Guid entryId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null) return;
        entry.SoftDelete();
        MarkUpdated();
    }

    public void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        MarkUpdated();
    }

    public IReadOnlyList<string> AllIngredients()
    {
        return _entries
            .Where(e => !e.IsDeleted)
            .SelectMany(e => e.IngredientList())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }
}
