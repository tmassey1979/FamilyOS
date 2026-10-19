using FamilyOS.Domain.Common;
using FamilyOS.Domain.Contracts;

namespace FamilyOS.Domain.Tasks;

public class TaskItem : Entity
{
    public Guid FamilyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public FamilyTaskStatus Status { get; private set; } = FamilyTaskStatus.Created;
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public Guid CreatedByMemberId { get; private set; }
    public Guid? AssigneeMemberId { get; private set; }
    public Guid? AcceptedByMemberId { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? PlannedStartUtc { get; private set; }
    public DateTime? PlannedEndUtc { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Category { get; private set; }
    public Guid? RecurrenceDefinitionId { get; private set; }
    public Guid? CalendarEventId { get; private set; }
    public bool CalendarBacked { get; private set; }

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
        string? category = null,
        DateTime? dueDate = null,
        Guid? recurrenceDefinitionId = null,
        TaskPriority priority = TaskPriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");

        var task = new TaskItem
        {
            FamilyId = familyId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            CreatedByMemberId = createdByMemberId,
            DueDateUtc = dueDate,
            RecurrenceDefinitionId = recurrenceDefinitionId,
            Priority = priority,
            Status = FamilyTaskStatus.Created
        };
        task.AddHistory(FamilyTaskStatus.Created, createdByMemberId, "Task created");
        return task;
    }

    public void Assign(Guid assigneeMemberId, Guid actorMemberId)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled or FamilyTaskStatus.Skipped)
            throw new InvalidOperationException($"Cannot assign from {Status}.");

        AssigneeMemberId = assigneeMemberId;
        AcceptedByMemberId = null;
        Status = FamilyTaskStatus.Assigned;
        AddHistory(FamilyTaskStatus.Assigned, actorMemberId, "Assigned");
        AddDomainEvent(new TaskAssigned(Id, FamilyId, assigneeMemberId, actorMemberId));
        MarkUpdated();
    }

    public void Accept(Guid memberId)
    {
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can accept.");
        if (Status != FamilyTaskStatus.Assigned)
            throw new InvalidOperationException($"Cannot accept from {Status}.");

        AcceptedByMemberId = memberId;
        Status = FamilyTaskStatus.Accepted;
        AddHistory(FamilyTaskStatus.Accepted, memberId, "Accepted");
        MarkUpdated();
    }

    public void Start(Guid memberId)
    {
        EnsureAssignee(memberId);
        if (Status is not (FamilyTaskStatus.Accepted or FamilyTaskStatus.Assigned or FamilyTaskStatus.InProgress))
            throw new InvalidOperationException($"Cannot start from {Status}.");

        Status = FamilyTaskStatus.InProgress;
        OpenSegment(memberId);
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Started");
        MarkUpdated();
    }

    public void Pause(Guid memberId)
    {
        EnsureAssignee(memberId);
        if (Status != FamilyTaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot pause from {Status}.");

        CloseOpenSegment(memberId);
        Status = FamilyTaskStatus.Paused;
        AddHistory(FamilyTaskStatus.Paused, memberId, "Paused");
        MarkUpdated();
    }

    public void Resume(Guid memberId)
    {
        EnsureAssignee(memberId);
        if (Status != FamilyTaskStatus.Paused)
            throw new InvalidOperationException($"Cannot resume from {Status}.");

        Status = FamilyTaskStatus.InProgress;
        OpenSegment(memberId);
        AddHistory(FamilyTaskStatus.InProgress, memberId, "Resumed");
        MarkUpdated();
    }

    public void Complete(Guid memberId)
    {
        EnsureAssignee(memberId);
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot complete from {Status}.");

        CloseOpenSegment(memberId);
        Status = FamilyTaskStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddHistory(FamilyTaskStatus.Completed, memberId, "Completed");
        AddDomainEvent(new TaskCompleted(Id, FamilyId, memberId, CompletedAtUtc.Value));
        MarkUpdated();
    }

    public void Decline(Guid memberId, string? note = null, Guid? declineReasonId = null)
    {
        EnsureAssignee(memberId);
        if (Status is not (FamilyTaskStatus.Assigned or FamilyTaskStatus.Accepted))
            throw new InvalidOperationException($"Cannot decline from {Status}.");

        Status = FamilyTaskStatus.Declined;
        var detail = string.IsNullOrWhiteSpace(note) ? "Declined" : $"Declined: {note}";
        AddHistory(FamilyTaskStatus.Declined, memberId, detail);
        MarkUpdated();
    }

    public void Defer(Guid memberId, DateTime untilUtc, string? note = null)
    {
        EnsureAssignee(memberId);
        if (Status != FamilyTaskStatus.Assigned)
            throw new InvalidOperationException($"Cannot defer from {Status}.");

        Status = FamilyTaskStatus.Deferred;
        DueDateUtc = untilUtc;
        var detail = string.IsNullOrWhiteSpace(note) ? $"Deferred until {untilUtc:u}" : $"Deferred until {untilUtc:u}: {note}";
        AddHistory(FamilyTaskStatus.Deferred, memberId, detail);
        MarkUpdated();
    }

    public void Reassign(Guid newAssigneeMemberId, Guid actorMemberId)
    {
        if (Status is not (FamilyTaskStatus.Assigned or FamilyTaskStatus.Declined or FamilyTaskStatus.Deferred))
            throw new InvalidOperationException($"Cannot reassign from {Status}.");

        AssigneeMemberId = newAssigneeMemberId;
        AcceptedByMemberId = null;
        Status = FamilyTaskStatus.Assigned;
        AddHistory(FamilyTaskStatus.Assigned, actorMemberId, "Reassigned");
        AddDomainEvent(new TaskAssigned(Id, FamilyId, newAssigneeMemberId, actorMemberId));
        MarkUpdated();
    }

    public void Skip(Guid actorMemberId, string? reason = null)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled or FamilyTaskStatus.Skipped)
            throw new InvalidOperationException($"Cannot skip from {Status}.");

        CloseOpenSegment(actorMemberId);
        Status = FamilyTaskStatus.Skipped;
        var detail = string.IsNullOrWhiteSpace(reason) ? "Skipped" : $"Skipped: {reason}";
        AddHistory(FamilyTaskStatus.Skipped, actorMemberId, detail);
        MarkUpdated();
    }

    public void Cancel(Guid actorMemberId, string? reason = null)
    {
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel from {Status}.");

        CloseOpenSegment(actorMemberId);
        Status = FamilyTaskStatus.Cancelled;
        var detail = string.IsNullOrWhiteSpace(reason) ? "Cancelled" : $"Cancelled: {reason}";
        AddHistory(FamilyTaskStatus.Cancelled, actorMemberId, detail);
        MarkUpdated();
    }

    public void SetPriority(TaskPriority priority)
    {
        Priority = priority;
        MarkUpdated();
    }

    public void UpdateDetails(string title, string? description, string? category)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required.");
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        MarkUpdated();
    }

    public void SetDueDate(DateTime? dueDateUtc)
    {
        DueDateUtc = dueDateUtc;
        MarkUpdated();
    }

    public void SetPlannedTimes(DateTime? plannedStartUtc, DateTime? plannedEndUtc, int? estimatedMinutes)
    {
        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        EstimatedMinutes = estimatedMinutes;
        MarkUpdated();
    }

    public bool IsOverdue(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (Status is FamilyTaskStatus.Completed or FamilyTaskStatus.Cancelled or FamilyTaskStatus.Skipped)
            return false;
        return DueDateUtc.HasValue && DueDateUtc.Value < now;
    }

    private void EnsureAssignee(Guid memberId)
    {
        if (AssigneeMemberId != memberId)
            throw new UnauthorizedAccessException("Only the assignee can perform this action.");
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
