using FamilyOS.Contracts.Events;
using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Tasks;

public class TaskItem : Entity
{
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public FamilyTaskStatus Status { get; private set; } = FamilyTaskStatus.Created;
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public Guid CreatedByMemberId { get; private set; }
    public Guid? AssigneeMemberId { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? PlannedStartUtc { get; private set; }
    public DateTime? PlannedEndUtc { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid? RecurrenceDefinitionId { get; private set; }
    public bool CalendarBacked { get; private set; }
    public Guid? CalendarEventId { get; private set; }

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
        Guid? recurrenceDefinitionId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        var task = new TaskItem
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            CreatedByMemberId = createdByMemberId,
            Priority = priority,
            DueDate = dueDate,
            RecurrenceDefinitionId = recurrenceDefinitionId,
            Status = FamilyTaskStatus.Created
        };

        task.AddHistory(FamilyTaskStatus.Created, createdByMemberId, "Task created");
        return task;
    }

    public void Assign(Guid assigneeMemberId, Guid actorMemberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot assign a task in status {Status}.");

        AssigneeMemberId = assigneeMemberId;
        Status = FamilyTaskStatus.Assigned;
        AddHistory(FamilyTaskStatus.Assigned, actorMemberId, $"Assigned to member {assigneeMemberId}");
        AddDomainEvent(new TaskAssigned(Id, FamilyId, assigneeMemberId, Title));
        MarkUpdated();
    }

    /// <summary>Assignment does not imply acceptance — assignee must explicitly accept.</summary>
    public void Accept(Guid memberId)
    {
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can accept this task.");
        if (Status != FamilyTaskStatus.Assigned && Status != FamilyTaskStatus.Reassigned)
            throw new InvalidOperationException($"Cannot accept task in status {Status}.");

        Status = FamilyTaskStatus.Accepted;
        AddHistory(FamilyTaskStatus.Accepted, memberId, "Task accepted");
        MarkUpdated();
    }

    public void Start(Guid memberId)
    {
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can start this task.");
        if (Status is not (FamilyTaskStatus.Accepted or FamilyTaskStatus.Assigned or FamilyTaskStatus.Deferred))
            throw new InvalidOperationException($"Cannot start task in status {Status}.");

        Status = FamilyTaskStatus.InProgress;
        OpenSegment(memberId);
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Work started");
        MarkUpdated();
    }

    public void Pause(Guid memberId)
    {
        if (Status != FamilyTaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot pause task in status {Status}.");
        CloseOpenSegment(memberId);
        Status = FamilyTaskStatus.Paused;
        AddHistory(FamilyTaskStatus.Paused, memberId, "Paused");
        MarkUpdated();
    }

    public void Resume(Guid memberId)
    {
        if (Status != FamilyTaskStatus.Paused)
            throw new InvalidOperationException($"Cannot resume task in status {Status}.");
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can resume.");
        Status = FamilyTaskStatus.InProgress;
        OpenSegment(memberId);
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Resumed");
        MarkUpdated();
    }

    public void Complete(Guid memberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Task is already {Status}.");

        CloseOpenSegment(memberId);
        Status = FamilyTaskStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddHistory(FamilyTaskStatus.Completed, memberId, "Task completed");
        AddDomainEvent(new TaskCompleted(Id, FamilyId, memberId, CompletedAtUtc.Value));
        MarkUpdated();
    }

    public void Decline(Guid memberId, Guid? declineReasonId = null, string? note = null)
    {
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can decline.");
        if (Status is not (FamilyTaskStatus.Assigned or FamilyTaskStatus.Reassigned or FamilyTaskStatus.Accepted))
            throw new InvalidOperationException($"Cannot decline task in status {Status}.");

        Status = FamilyTaskStatus.Declined;
        var detail = note is null ? "Declined" : $"Declined: {note}";
        AddHistory(FamilyTaskStatus.Declined, memberId, detail);
        MarkUpdated();
    }

    public void Defer(Guid memberId, DateTime newDueDate, string? note = null)
    {
        if (Status != FamilyTaskStatus.Assigned && Status != FamilyTaskStatus.Accepted && Status != FamilyTaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot defer task in status {Status}.");

        DueDate = newDueDate;
        Status = FamilyTaskStatus.Deferred;
        AddHistory(FamilyTaskStatus.Deferred, memberId, note ?? $"Deferred to {newDueDate:u}");
        MarkUpdated();
    }

    public void Reassign(Guid newAssigneeMemberId, Guid actorMemberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot reassign a task in status {Status}.");

        AssigneeMemberId = newAssigneeMemberId;
        Status = FamilyTaskStatus.Reassigned;
        AddHistory(FamilyTaskStatus.Reassigned, actorMemberId, $"Reassigned to {newAssigneeMemberId}");
        AddDomainEvent(new TaskAssigned(Id, FamilyId, newAssigneeMemberId, Title));
        MarkUpdated();
    }

    public void Cancel(Guid actorMemberId, string? reason = null)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a task in status {Status}.");

        CloseOpenSegment(actorMemberId);
        Status = FamilyTaskStatus.Cancelled;
        AddHistory(FamilyTaskStatus.Cancelled, actorMemberId, reason ?? "Cancelled");
        MarkUpdated();
    }

    public void Skip(Guid actorMemberId, string? reason = null)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled or FamilyTaskStatus.Skipped)
            throw new InvalidOperationException($"Cannot skip a task in status {Status}.");

        CloseOpenSegment(actorMemberId);
        Status = FamilyTaskStatus.Skipped;
        AddHistory(FamilyTaskStatus.Skipped, actorMemberId, reason ?? "Skipped this occurrence");
        MarkUpdated();
    }

    public void SetPriority(TaskPriority priority)
    {
        Priority = priority;
        MarkUpdated();
    }

    public void UpdateDetails(string? title = null, string? description = null, string? category = null)
    {
        if (title is not null)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));
            Title = title.Trim();
        }
        if (description is not null)
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (category is not null)
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        MarkUpdated();
    }

    public void SetDueDate(DateTime? dueDate)
    {
        DueDate = dueDate;
        MarkUpdated();
    }

    public void SetPlannedTimes(DateTime? plannedStartUtc, DateTime? plannedEndUtc, int? estimatedMinutes = null)
    {
        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        if (estimatedMinutes.HasValue)
            EstimatedMinutes = estimatedMinutes;
        MarkUpdated();
    }

    public void EnableCalendarBacking(Guid calendarEventId)
    {
        CalendarBacked = true;
        CalendarEventId = calendarEventId;
        MarkUpdated();
    }

    public bool IsOverdue(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled or FamilyTaskStatus.Skipped)
            return false;
        return DueDate.HasValue && DueDate.Value < now;
    }

    private void OpenSegment(Guid memberId)
    {
        if (_timeSegments.Any(s => s.EndedAtUtc is null))
            return;
        _timeSegments.Add(TaskTimeSegment.Start(Id, memberId));
    }

    private void CloseOpenSegment(Guid memberId)
    {
        var open = _timeSegments.LastOrDefault(s => s.EndedAtUtc is null);
        open?.End(memberId);
    }

    private void AddHistory(FamilyTaskStatus status, Guid actorMemberId, string detail)
    {
        _history.Add(TaskHistoryEntry.Create(Id, status, actorMemberId, detail));
    }
}
