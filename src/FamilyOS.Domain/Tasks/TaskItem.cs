using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Tasks;

/// <summary>
/// First-class execution object. Assignment ≠ Acceptance.
/// </summary>
public class TaskItem : Entity
{
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedByMemberId { get; private set; }
    public Guid? AssignedToMemberId { get; private set; }
    public FamilyTaskStatus Status { get; private set; } = FamilyTaskStatus.Created;
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public DateTime? DueDate { get; private set; }
    public DateTime? StartDate { get; private set; }
    public TimeOnly? PlannedStartTime { get; private set; }
    public TimeOnly? PlannedEndTime { get; private set; }
    public int? EstimatedDurationMinutes { get; private set; }
    public int? ActualDurationMinutes { get; private set; }
    public string? Category { get; private set; }
    public string? Location { get; private set; }
    public bool CalendarBacked { get; private set; }
    public Guid? CalendarEventId { get; private set; }
    public Guid? RecurrenceDefinitionId { get; private set; }
    public Guid? ParentRequestId { get; private set; }
    public Guid? ParentExecutionItemId { get; private set; }

    private readonly List<TaskHistoryEntry> _history = new();
    public IReadOnlyCollection<TaskHistoryEntry> History => _history.AsReadOnly();

    private readonly List<TaskTimeSegment> _timeSegments = new();
    public IReadOnlyCollection<TaskTimeSegment> TimeSegments => _timeSegments.AsReadOnly();

    private TaskItem() { }

    public static TaskItem Create(
        Guid familyId,
        string title,
        Guid createdByMemberId,
        string? description = null,
        TaskPriority priority = TaskPriority.Normal,
        DateTime? dueDate = null,
        string? category = null,
        bool calendarBacked = false,
        Guid? recurrenceDefinitionId = null,
        Guid? parentRequestId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var task = new TaskItem
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Description = description?.Trim(),
            CreatedByMemberId = createdByMemberId,
            Priority = priority,
            DueDate = dueDate,
            Category = category,
            CalendarBacked = calendarBacked,
            RecurrenceDefinitionId = recurrenceDefinitionId,
            ParentRequestId = parentRequestId,
            Status = FamilyTaskStatus.Created
        };

        task.AddHistory(FamilyTaskStatus.Created, createdByMemberId, "Task created");
        return task;
    }

    public void Assign(Guid assigneeMemberId, Guid actorMemberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot assign a task in status {Status}.");

        AssignedToMemberId = assigneeMemberId;
        Status = FamilyTaskStatus.Assigned;
        AddHistory(FamilyTaskStatus.Assigned, actorMemberId, $"Assigned to member {assigneeMemberId}");
        MarkUpdated();
    }

    public void Accept(Guid memberId)
    {
        EnsureAssignedTo(memberId);
        if (Status != FamilyTaskStatus.Assigned && Status != FamilyTaskStatus.Reassigned)
            throw new InvalidOperationException($"Task must be Assigned to accept. Current: {Status}");

        Status = FamilyTaskStatus.Accepted;
        AddHistory(FamilyTaskStatus.Accepted, memberId, "Task accepted");
        MarkUpdated();
    }

    public void Decline(Guid memberId, Guid? reasonId = null, string? note = null)
    {
        EnsureAssignedTo(memberId);
        if (Status is not (FamilyTaskStatus.Assigned or FamilyTaskStatus.Accepted or FamilyTaskStatus.Reassigned))
            throw new InvalidOperationException($"Cannot decline task in status {Status}.");

        Status = FamilyTaskStatus.Declined;
        var detail = reasonId.HasValue ? $"ReasonId={reasonId}" : "No reason";
        if (!string.IsNullOrWhiteSpace(note)) detail += $"; Note={note}";
        AddHistory(FamilyTaskStatus.Declined, memberId, detail);
        MarkUpdated();
    }

    public void Start(Guid memberId)
    {
        EnsureAssignedTo(memberId);
        if (Status is not (FamilyTaskStatus.Accepted or FamilyTaskStatus.Paused or FamilyTaskStatus.Deferred))
            throw new InvalidOperationException($"Cannot start task in status {Status}.");

        Status = FamilyTaskStatus.InProgress;
        _timeSegments.Add(TaskTimeSegment.Start(Id, memberId));
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Work started");
        MarkUpdated();
    }

    public void Pause(Guid memberId)
    {
        EnsureAssignedTo(memberId);
        if (Status != FamilyTaskStatus.InProgress)
            throw new InvalidOperationException("Task is not in progress.");

        Status = FamilyTaskStatus.Paused;
        CloseOpenSegment(memberId);
        AddHistory(FamilyTaskStatus.Paused, memberId, "Work paused");
        MarkUpdated();
    }

    public void Resume(Guid memberId)
    {
        EnsureAssignedTo(memberId);
        if (Status != FamilyTaskStatus.Paused)
            throw new InvalidOperationException("Task is not paused.");

        Status = FamilyTaskStatus.InProgress;
        _timeSegments.Add(TaskTimeSegment.Start(Id, memberId));
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Work resumed");
        MarkUpdated();
    }

    public void Complete(Guid memberId)
    {
        EnsureAssignedTo(memberId);
        if (Status is not (FamilyTaskStatus.InProgress or FamilyTaskStatus.Accepted or FamilyTaskStatus.Paused))
            throw new InvalidOperationException($"Cannot complete task in status {Status}.");

        if (Status == FamilyTaskStatus.InProgress)
            CloseOpenSegment(memberId);

        Status = FamilyTaskStatus.Completed;
        ActualDurationMinutes = _timeSegments
            .Where(s => s.EndedAtUtc.HasValue)
            .Sum(s => (int)(s.EndedAtUtc!.Value - s.StartedAtUtc).TotalMinutes);

        AddHistory(FamilyTaskStatus.Completed, memberId, $"Completed. Actual duration: {ActualDurationMinutes} min");
        MarkUpdated();
    }

    public void Defer(Guid memberId, string? note = null)
    {
        EnsureAssignedTo(memberId);
        Status = FamilyTaskStatus.Deferred;
        AddHistory(FamilyTaskStatus.Deferred, memberId, note ?? "Deferred");
        MarkUpdated();
    }

    public void Cancel(Guid actorMemberId, string? reason = null)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel task in status {Status}.");

        Status = FamilyTaskStatus.Cancelled;
        AddHistory(FamilyTaskStatus.Cancelled, actorMemberId, reason ?? "Cancelled");
        MarkUpdated();
    }

    public void Reassign(Guid newAssigneeMemberId, Guid actorMemberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot reassign task in status {Status}.");

        var previous = AssignedToMemberId;
        AssignedToMemberId = newAssigneeMemberId;
        Status = FamilyTaskStatus.Reassigned;
        AddHistory(FamilyTaskStatus.Reassigned, actorMemberId, $"Reassigned from {previous} to {newAssigneeMemberId}");
        Status = FamilyTaskStatus.Assigned;
        MarkUpdated();
    }

    public void SetPlannedTimes(TimeOnly? start, TimeOnly? end, int? estimatedMinutes)
    {
        PlannedStartTime = start;
        PlannedEndTime = end;
        EstimatedDurationMinutes = estimatedMinutes;
        MarkUpdated();
    }

    public void LinkCalendarEvent(Guid calendarEventId)
    {
        CalendarEventId = calendarEventId;
        CalendarBacked = true;
        MarkUpdated();
    }

    private void EnsureAssignedTo(Guid memberId)
    {
        if (AssignedToMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assigned member can perform this action.");
    }

    private void CloseOpenSegment(Guid memberId)
    {
        var open = _timeSegments.LastOrDefault(s => !s.EndedAtUtc.HasValue);
        open?.End(memberId);
    }

    private void AddHistory(FamilyTaskStatus status, Guid actorMemberId, string detail)
    {
        _history.Add(TaskHistoryEntry.Create(Id, status, actorMemberId, detail));
    }
}
