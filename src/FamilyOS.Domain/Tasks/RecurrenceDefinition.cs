using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Tasks;

/// <summary>
/// Recurring task definition that generates independent task instances.
/// </summary>
public class RecurrenceDefinition : Entity
{
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    public int Interval { get; private set; } = 1;
    public DayOfWeek? DayOfWeek { get; private set; }
    public int? DayOfMonth { get; private set; }
    public TimeOnly? PreferredTime { get; private set; }
    public Guid? DefaultAssigneeMemberId { get; private set; }
    public DeclinePolicy DeclinePolicy { get; private set; } = DeclinePolicy.NoChange;
    public bool IsActive { get; private set; } = true;
    public DateTime? NextOccurrenceUtc { get; private set; }
    public string? Category { get; private set; }

    private RecurrenceDefinition() { }

    public static RecurrenceDefinition Create(
        Guid familyId,
        string title,
        RecurrenceFrequency frequency,
        int interval = 1,
        DayOfWeek? dayOfWeek = null,
        Guid? defaultAssignee = null,
        string? category = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");

        return new RecurrenceDefinition
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Frequency = frequency,
            Interval = Math.Max(1, interval),
            DayOfWeek = dayOfWeek,
            DefaultAssigneeMemberId = defaultAssignee,
            Category = category,
            NextOccurrenceUtc = DateTime.UtcNow.Date
        };
    }

    public TaskItem GenerateInstance(Guid createdByMemberId, DateTime occurrenceDate)
    {
        var task = TaskItem.Create(
            FamilyId,
            Title,
            createdByMemberId,
            Description,
            category: Category,
            dueDate: occurrenceDate,
            recurrenceDefinitionId: Id);

        if (DefaultAssigneeMemberId.HasValue)
            task.Assign(DefaultAssigneeMemberId.Value, createdByMemberId);

        AdvanceNextOccurrence();
        return task;
    }

    private void AdvanceNextOccurrence()
    {
        if (!NextOccurrenceUtc.HasValue) return;
        var next = NextOccurrenceUtc.Value;
        next = Frequency switch
        {
            RecurrenceFrequency.Daily => next.AddDays(Interval),
            RecurrenceFrequency.Weekly => next.AddDays(7 * Interval),
            RecurrenceFrequency.BiWeekly => next.AddDays(14),
            RecurrenceFrequency.Monthly => next.AddMonths(Interval),
            RecurrenceFrequency.Yearly => next.AddYears(Interval),
            _ => next.AddDays(Interval)
        };
        NextOccurrenceUtc = next;
        MarkUpdated();
    }

    public void SetDeclinePolicy(DeclinePolicy policy)
    {
        DeclinePolicy = policy;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
