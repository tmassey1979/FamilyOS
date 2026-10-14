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
        string? category = null,
        string? description = null,
        DateTime? firstOccurrenceUtc = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");
        if (frequency == RecurrenceFrequency.None)
            throw new ArgumentException("Frequency must not be None.", nameof(frequency));

        var first = firstOccurrenceUtc?.Date ?? DateTime.UtcNow.Date;
        if (frequency is RecurrenceFrequency.Weekly or RecurrenceFrequency.BiWeekly && dayOfWeek.HasValue)
        {
            while (first.DayOfWeek != dayOfWeek.Value)
                first = first.AddDays(1);
        }

        return new RecurrenceDefinition
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Frequency = frequency,
            Interval = Math.Max(1, interval),
            DayOfWeek = dayOfWeek,
            DefaultAssigneeMemberId = defaultAssignee,
            Category = category,
            NextOccurrenceUtc = first
        };
    }

    /// <summary>
    /// Create the next open task instance and advance the schedule cursor.
    /// </summary>
    public TaskItem GenerateInstance(Guid createdByMemberId, DateTime? occurrenceDate = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot generate instances from an inactive recurrence.");

        var when = (occurrenceDate ?? NextOccurrenceUtc ?? DateTime.UtcNow).Date;
        if (PreferredTime.HasValue)
            when = when.Add(PreferredTime.Value.ToTimeSpan());

        var task = TaskItem.Create(
            FamilyId,
            Title,
            createdByMemberId,
            Description,
            category: Category,
            dueDate: when,
            recurrenceDefinitionId: Id);

        if (DefaultAssigneeMemberId.HasValue)
            task.Assign(DefaultAssigneeMemberId.Value, createdByMemberId);

        AdvanceNextOccurrence(from: when.Date);
        return task;
    }

    public static DateTime ComputeNext(DateTime from, RecurrenceFrequency frequency, int interval, DayOfWeek? dayOfWeek = null)
    {
        interval = Math.Max(1, interval);
        var next = frequency switch
        {
            RecurrenceFrequency.Daily => from.AddDays(interval),
            RecurrenceFrequency.Weekly => from.AddDays(7 * interval),
            RecurrenceFrequency.BiWeekly => from.AddDays(14),
            RecurrenceFrequency.Monthly => from.AddMonths(interval),
            RecurrenceFrequency.Yearly => from.AddYears(interval),
            _ => from.AddDays(interval)
        };

        if (frequency is RecurrenceFrequency.Weekly or RecurrenceFrequency.BiWeekly && dayOfWeek.HasValue)
        {
            // Snap forward to the preferred weekday if we landed off-day (e.g. first advance).
            var guard = 0;
            while (next.DayOfWeek != dayOfWeek.Value && guard++ < 7)
                next = next.AddDays(1);
        }

        return next.Date;
    }

    private void AdvanceNextOccurrence(DateTime? from = null)
    {
        var cursor = from ?? NextOccurrenceUtc?.Date ?? DateTime.UtcNow.Date;
        NextOccurrenceUtc = ComputeNext(cursor, Frequency, Interval, DayOfWeek);
        MarkUpdated();
    }

    public void SetDeclinePolicy(DeclinePolicy policy)
    {
        DeclinePolicy = policy;
        MarkUpdated();
    }

    public void SetDefaultAssignee(Guid? memberId)
    {
        DefaultAssigneeMemberId = memberId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        if (!NextOccurrenceUtc.HasValue)
            NextOccurrenceUtc = DateTime.UtcNow.Date;
        MarkUpdated();
    }
}
