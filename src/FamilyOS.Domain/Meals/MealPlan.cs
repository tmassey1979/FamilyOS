using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Meals;

/// <summary>One household week of meals (week starts Monday UTC date).</summary>
public class MealPlan : Entity
{
    public Guid FamilyId { get; private set; }
    public DateTime WeekStartUtc { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<MealEntry> _entries = new();
    public IReadOnlyCollection<MealEntry> Entries => _entries.AsReadOnly();

    private MealPlan() { }

    public static MealPlan Create(Guid familyId, DateTime weekStartUtc, string? notes = null)
    {
        var monday = ToMonday(weekStartUtc);
        return new MealPlan
        {
            FamilyId = familyId,
            WeekStartUtc = monday,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public static DateTime ToMonday(DateTime any)
    {
        var d = any.Date;
        var diff = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return d.AddDays(-diff);
    }

    public MealEntry UpsertEntry(DateTime dayUtc, MealSlot slot, string dishName, string? notes = null, string? ingredientsCsv = null)
    {
        if (string.IsNullOrWhiteSpace(dishName))
            throw new ArgumentException("Dish name required.", nameof(dishName));

        var day = dayUtc.Date;
        if (day < WeekStartUtc || day > WeekStartUtc.AddDays(6))
            throw new InvalidOperationException("Entry day must fall within the plan week.");

        var existing = _entries.FirstOrDefault(e => e.DayUtc.Date == day && e.Slot == slot && !e.IsDeleted);
        if (existing is not null)
        {
            existing.Update(dishName, notes, ingredientsCsv);
            MarkUpdated();
            return existing;
        }

        var entry = MealEntry.Create(Id, FamilyId, day, slot, dishName, notes, ingredientsCsv);
        _entries.Add(entry);
        MarkUpdated();
        return entry;
    }

    public void RemoveEntry(Guid entryId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Meal entry not found.");
        entry.SoftDelete();
        MarkUpdated();
    }

    public void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        MarkUpdated();
    }
}
