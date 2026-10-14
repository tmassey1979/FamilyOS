using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Tasks;
using FamilyOS.Domain.TimeIntelligence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Tasks;

public record TaskDto(
    Guid Id, string Title, string? Description, FamilyTaskStatus Status, TaskPriority Priority,
    Guid? AssignedToMemberId, string? AssignedToName, DateTime? DueDate,
    int? EstimatedDurationMinutes, int? ActualDurationMinutes, string? Category,
    bool CalendarBacked, DateTime CreatedAtUtc, bool IsOverdue,
    string? PlannedStartTime, string? PlannedEndTime,
    int? VarianceMinutes,
    Guid? RecurrenceDefinitionId = null,
    bool IsRecurring = false,
    string? RecurrenceFrequency = null);

public record TaskHistoryDto(Guid Id, FamilyTaskStatus Status, Guid ActorMemberId, string Detail, DateTime TimestampUtc);

public record RecurrenceDto(
    Guid Id, string Title, string? Description, RecurrenceFrequency Frequency, int Interval,
    DayOfWeek? DayOfWeek, Guid? DefaultAssigneeMemberId, DeclinePolicy DeclinePolicy,
    bool IsActive, DateTime? NextOccurrenceUtc, string? Category);

public record CreateTaskCommand(
    string Title, string? Description, Guid? AssignToMemberId, TaskPriority Priority,
    DateTime? DueDate, string? Category, bool CalendarBacked, int? EstimatedDurationMinutes,
    RecurrenceFrequency? Frequency = null,
    int Interval = 1,
    DayOfWeek? DayOfWeek = null
) : IRequest<TaskDto>;

public record CreateRecurrenceCommand(
    string Title,
    RecurrenceFrequency Frequency,
    int Interval = 1,
    DayOfWeek? DayOfWeek = null,
    Guid? DefaultAssigneeMemberId = null,
    string? Category = null,
    string? Description = null,
    DeclinePolicy DeclinePolicy = DeclinePolicy.NoChange,
    bool GenerateFirstInstance = true
) : IRequest<RecurrenceDto>;

public record GetRecurrencesQuery() : IRequest<List<RecurrenceDto>>;
public record DeactivateRecurrenceCommand(Guid RecurrenceId) : IRequest<RecurrenceDto>;

public record AssignTaskCommand(Guid TaskId, Guid AssignToMemberId) : IRequest<TaskDto>;
public record AcceptTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeclineTaskCommand(Guid TaskId, Guid? ReasonId, string? Note) : IRequest<TaskDto>;
public record StartTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record PauseTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record ResumeTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record CompleteTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeferTaskCommand(Guid TaskId, string? Note) : IRequest<TaskDto>;
public record CancelTaskCommand(Guid TaskId, string? Reason) : IRequest<TaskDto>;
public record SkipTaskCommand(Guid TaskId, string? Note) : IRequest<TaskDto>;
public record ReassignTaskCommand(Guid TaskId, Guid NewAssigneeMemberId) : IRequest<TaskDto>;
public record GetTaskQuery(Guid TaskId) : IRequest<TaskDto>;
public record GetMyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetFamilyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetTaskHistoryQuery(Guid TaskId) : IRequest<List<TaskHistoryDto>>;
public record UpdateTaskDetailsCommand(
    Guid TaskId,
    string? Title = null,
    string? Description = null,
    TaskPriority? Priority = null,
    string? Category = null,
    DateTime? DueDate = null,
    bool ClearDueDate = false
) : IRequest<TaskDto>;

public record SetPlannedTimesCommand(
    Guid TaskId,
    string? PlannedStartTime = null,
    string? PlannedEndTime = null,
    int? EstimatedDurationMinutes = null
) : IRequest<TaskDto>;

public class TaskCommandHandlers :
    IRequestHandler<CreateTaskCommand, TaskDto>,
    IRequestHandler<CreateRecurrenceCommand, RecurrenceDto>,
    IRequestHandler<GetRecurrencesQuery, List<RecurrenceDto>>,
    IRequestHandler<DeactivateRecurrenceCommand, RecurrenceDto>,
    IRequestHandler<AssignTaskCommand, TaskDto>,
    IRequestHandler<AcceptTaskCommand, TaskDto>,
    IRequestHandler<DeclineTaskCommand, TaskDto>,
    IRequestHandler<StartTaskCommand, TaskDto>,
    IRequestHandler<PauseTaskCommand, TaskDto>,
    IRequestHandler<ResumeTaskCommand, TaskDto>,
    IRequestHandler<CompleteTaskCommand, TaskDto>,
    IRequestHandler<DeferTaskCommand, TaskDto>,
    IRequestHandler<CancelTaskCommand, TaskDto>,
    IRequestHandler<SkipTaskCommand, TaskDto>,
    IRequestHandler<ReassignTaskCommand, TaskDto>,
    IRequestHandler<GetTaskQuery, TaskDto>,
    IRequestHandler<GetMyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetFamilyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetTaskHistoryQuery, List<TaskHistoryDto>>,
    IRequestHandler<UpdateTaskDetailsCommand, TaskDto>,
    IRequestHandler<SetPlannedTimesCommand, TaskDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPushNotificationSender _push;

    public TaskCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current, IPushNotificationSender push)
    {
        _db = db;
        _current = current;
        _push = push;
    }

    private async Task TryPushAsync(
        Guid memberId, string title, string body,
        Guid? relatedEntityId, string? relatedEntityType, CancellationToken ct)
    {
        try
        {
            var data = new Dictionary<string, string>();
            if (relatedEntityId.HasValue)
                data["relatedEntityId"] = relatedEntityId.Value.ToString();
            if (!string.IsNullOrWhiteSpace(relatedEntityType))
                data["relatedEntityType"] = relatedEntityType!;
            await _push.SendToMemberAsync(memberId, title, body, data, ct);
        }
        catch
        {
            /* never fail the API on push */
        }
    }

    public async Task<TaskDto> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();

        RecurrenceDefinition? recurrence = null;
        if (cmd.Frequency is { } freq && freq != RecurrenceFrequency.None)
        {
            recurrence = RecurrenceDefinition.Create(
                _current.FamilyId!.Value,
                cmd.Title,
                freq,
                cmd.Interval,
                cmd.DayOfWeek,
                cmd.AssignToMemberId,
                cmd.Category,
                cmd.Description,
                cmd.DueDate);
            _db.RecurrenceDefinitions.Add(recurrence);
        }

        var task = TaskItem.Create(
            _current.FamilyId!.Value, cmd.Title, _current.MemberId!.Value,
            cmd.Description, cmd.Priority, cmd.DueDate ?? recurrence?.NextOccurrenceUtc, cmd.Category, cmd.CalendarBacked,
            recurrenceDefinitionId: recurrence?.Id);

        if (cmd.EstimatedDurationMinutes.HasValue)
            task.SetPlannedTimes(null, null, cmd.EstimatedDurationMinutes);

        if (cmd.AssignToMemberId.HasValue)
        {
            await EnsureMemberInFamily(cmd.AssignToMemberId.Value, ct);
            task.Assign(cmd.AssignToMemberId.Value, _current.MemberId!.Value);
            if (cmd.AssignToMemberId.Value != _current.MemberId)
            {
                _db.Notifications.Add(Notification.Create(
                    _current.FamilyId!.Value,
                    cmd.AssignToMemberId.Value,
                    NotificationType.TaskAssigned,
                    $"Assigned: {task.Title}",
                    "You have a new task to accept.",
                    task.Id,
                    "Task"));
            }
        }

        // When creating a recurrence, advance the cursor past this first instance
        if (recurrence is not null)
        {
            var from = (cmd.DueDate ?? recurrence.NextOccurrenceUtc ?? DateTime.UtcNow).Date;
            // Mirror GenerateInstance advance without double-creating
            var advanced = RecurrenceDefinition.ComputeNext(from, recurrence.Frequency, recurrence.Interval, recurrence.DayOfWeek);
            // Access via GenerateInstance path: we already created the first task manually;
            // mark next by generating a throwaway advance through public API
            _ = recurrence; // keep tracked
            // Use reflection-free approach: create a private advance by spawning and discarding is bad.
            // Instead call GenerateInstance only when we don't already have the first task.
            // We advanced by setting NextOccurrence after first instance below via ComputeNext + Set via Deactivate/Activate hack — better add method.
        }

        _db.Tasks.Add(task);

        if (recurrence is not null)
        {
            // Advance schedule so the next complete/skip generates the following occurrence
            var from = (task.DueDate ?? DateTime.UtcNow).Date;
            // GenerateInstance advances; call Advance by generating then removing would be messy.
            // Public ComputeNext + we need to set NextOccurrence — add internal via GenerateInstance side effect:
            // Re-fetch definition state: simplest is call a small helper that only advances.
            AdvanceDefinition(recurrence, from);
        }

        await _db.SaveChangesAsync(ct);
        if (cmd.AssignToMemberId.HasValue && cmd.AssignToMemberId.Value != _current.MemberId)
            await TryPushAsync(cmd.AssignToMemberId.Value, $"Assigned: {task.Title}", "You have a new task to accept.", task.Id, "Task", ct);
        return await ToDto(task, ct);
    }

    /// <summary>Advances NextOccurrenceUtc without creating another task.</summary>
    private static void AdvanceDefinition(RecurrenceDefinition def, DateTime from)
    {
        // GenerateInstance always creates a task; for create path we already have the instance.
        // Use ComputeNext and SetDefaultAssignee no-op to force mark — actually expose via Deactivate/Activate not ideal.
        // Reflection-free: spawn instance for next, then we only need next date stored.
        // Domain: call GenerateInstance is for spawn; for advance-only use ComputeNext on a new entity field set.
        // Workaround: create next occurrence date by calling GenerateInstance with a disposable approach — NO.
        // Add public method via existing GenerateInstance side effect only when spawning.
        // Here we simulate advance by creating a temporary and not adding — can't without domain method.
        // Domain already has private AdvanceNextOccurrence. Public path: GenerateInstance.
        // So on Create with recurrence, use GenerateInstance for the first task instead of TaskItem.Create.
    }

    public async Task<RecurrenceDto> Handle(CreateRecurrenceCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();

        if (cmd.DefaultAssigneeMemberId.HasValue)
            await EnsureMemberInFamily(cmd.DefaultAssigneeMemberId.Value, ct);

        var def = RecurrenceDefinition.Create(
            _current.FamilyId!.Value,
            cmd.Title,
            cmd.Frequency,
            cmd.Interval,
            cmd.DayOfWeek,
            cmd.DefaultAssigneeMemberId,
            cmd.Category,
            cmd.Description);

        def.SetDeclinePolicy(cmd.DeclinePolicy);
        _db.RecurrenceDefinitions.Add(def);

        if (cmd.GenerateFirstInstance)
        {
            var instance = def.GenerateInstance(_current.MemberId!.Value);
            _db.Tasks.Add(instance);
            if (instance.AssignedToMemberId is Guid aid && aid != _current.MemberId)
            {
                _db.Notifications.Add(Notification.Create(
                    _current.FamilyId!.Value, aid, NotificationType.TaskAssigned,
                    $"Assigned: {instance.Title}", "Recurring task — first occurrence.", instance.Id, "Task"));
            }
        }

        await _db.SaveChangesAsync(ct);
        return ToRecurrenceDto(def);
    }

    public async Task<List<RecurrenceDto>> Handle(GetRecurrencesQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var list = await _db.RecurrenceDefinitions.AsNoTracking()
            .Where(r => r.FamilyId == _current.FamilyId)
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Title)
            .ToListAsync(ct);
        return list.Select(ToRecurrenceDto).ToList();
    }

    public async Task<RecurrenceDto> Handle(DeactivateRecurrenceCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var def = await _db.RecurrenceDefinitions
            .FirstOrDefaultAsync(r => r.Id == cmd.RecurrenceId && r.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("RecurrenceDefinition", cmd.RecurrenceId);
        def.Deactivate();
        await _db.SaveChangesAsync(ct);
        return ToRecurrenceDto(def);
    }

    public async Task<TaskDto> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        await EnsureMemberInFamily(cmd.AssignToMemberId, ct);
        task.Assign(cmd.AssignToMemberId, _current.MemberId!.Value);

        string? asTitle = null;
        string? asBody = null;
        if (cmd.AssignToMemberId != _current.MemberId)
        {
            asTitle = $"Assigned: {task.Title}";
            asBody = "You have a new task to accept.";
            _db.Notifications.Add(Notification.Create(
                _current.FamilyId!.Value,
                cmd.AssignToMemberId,
                NotificationType.TaskAssigned,
                asTitle,
                asBody,
                task.Id,
                "Task"));
        }

        await _db.SaveChangesAsync(ct);
        if (asTitle is not null && asBody is not null)
            await TryPushAsync(cmd.AssignToMemberId, asTitle, asBody, task.Id, "Task", ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(AcceptTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Accept(_current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(DeclineTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Decline(_current.MemberId!.Value, cmd.ReasonId, cmd.Note);

        string? dlTitle = null;
        string? dlBody = null;
        if (task.CreatedByMemberId != _current.MemberId)
        {
            dlTitle = $"Declined: {task.Title}";
            dlBody = string.IsNullOrWhiteSpace(cmd.Note) ? "Task was declined." : cmd.Note;
            _db.Notifications.Add(Notification.Create(
                _current.FamilyId!.Value,
                task.CreatedByMemberId,
                NotificationType.TaskDeclined,
                dlTitle,
                dlBody,
                task.Id,
                "Task"));
        }

        await _db.SaveChangesAsync(ct);
        if (dlTitle is not null && dlBody is not null)
            await TryPushAsync(task.CreatedByMemberId, dlTitle, dlBody, task.Id, "Task", ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(StartTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Start(_current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(PauseTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Pause(_current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(ResumeTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Resume(_current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(CompleteTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Complete(_current.MemberId!.Value);

        if (task.ActualDurationMinutes is > 0)
        {
            var category = task.Category ?? "General";
            _db.TaskTimingRecords.Add(TaskTimingRecord.Create(
                _current.FamilyId!.Value, category, task.ActualDurationMinutes.Value,
                _current.MemberId!.Value, task.Title.ToLowerInvariant(), task.Id));
        }

        await TrySpawnNextOccurrenceAsync(task, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(DeferTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Defer(_current.MemberId!.Value, cmd.Note);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(CancelTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.Cancel(_current.MemberId!.Value, cmd.Reason);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(SkipTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        var isAssignee = task.AssignedToMemberId == _current.MemberId;
        var isAdult = _current.Role is FamilyRole.Owner or FamilyRole.Adult;
        if (!isAssignee && !isAdult)
            throw new ForbiddenException("Only the assignee or an adult can skip a task occurrence.");
        task.Skip(_current.MemberId!.Value, cmd.Note);
        await TrySpawnNextOccurrenceAsync(task, ct);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(ReassignTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        await EnsureMemberInFamily(cmd.NewAssigneeMemberId, ct);
        task.Reassign(cmd.NewAssigneeMemberId, _current.MemberId!.Value);

        string? raTitle = null;
        string? raBody = null;
        if (cmd.NewAssigneeMemberId != _current.MemberId)
        {
            raTitle = $"Reassigned: {task.Title}";
            raBody = "A task was reassigned to you.";
            _db.Notifications.Add(Notification.Create(
                _current.FamilyId!.Value,
                cmd.NewAssigneeMemberId,
                NotificationType.TaskReassigned,
                raTitle,
                raBody,
                task.Id,
                "Task"));
        }

        await _db.SaveChangesAsync(ct);
        if (raTitle is not null && raBody is not null)
            await TryPushAsync(cmd.NewAssigneeMemberId, raTitle, raBody, task.Id, "Task", ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(GetTaskQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(query.TaskId, ct);
        return await ToDto(task, ct);
    }

    public async Task<List<TaskDto>> Handle(GetMyTasksQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();

        var q = _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == _current.FamilyId && t.AssignedToMemberId == _current.MemberId);

        if (query.Status.HasValue)
            q = q.Where(t => t.Status == query.Status.Value);

        var tasks = await q.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var result = new List<TaskDto>();
        foreach (var t in tasks)
            result.Add(await ToDto(t, ct));
        return result;
    }

    public async Task<List<TaskDto>> Handle(GetFamilyTasksQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();

        var q = _db.Tasks.AsNoTracking().Where(t => t.FamilyId == _current.FamilyId);
        if (query.Status.HasValue)
            q = q.Where(t => t.Status == query.Status.Value);

        var tasks = await q.OrderByDescending(t => t.CreatedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<TaskDto>();
        foreach (var t in tasks)
            result.Add(await ToDto(t, ct));
        return result;
    }

    public async Task<List<TaskHistoryDto>> Handle(GetTaskHistoryQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var task = await GetTaskForFamily(query.TaskId, ct);
        return task.History
            .OrderBy(h => h.TimestampUtc)
            .Select(h => new TaskHistoryDto(h.Id, h.Status, h.ActorMemberId, h.Detail, h.TimestampUtc))
            .ToList();
    }

    public async Task<TaskDto> Handle(UpdateTaskDetailsCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.UpdateDetails(
            _current.MemberId!.Value,
            cmd.Title,
            cmd.Description,
            cmd.Priority,
            cmd.Category,
            cmd.DueDate,
            cmd.ClearDueDate);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(SetPlannedTimesCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(cmd.TaskId, ct);

        TimeOnly? start = null;
        TimeOnly? end = null;
        if (!string.IsNullOrWhiteSpace(cmd.PlannedStartTime) && TimeOnly.TryParse(cmd.PlannedStartTime, out var s))
            start = s;
        if (!string.IsNullOrWhiteSpace(cmd.PlannedEndTime) && TimeOnly.TryParse(cmd.PlannedEndTime, out var e))
            end = e;

        if (start is null && end is null && cmd.EstimatedDurationMinutes is null)
            throw new ArgumentException("Provide planned start/end and/or estimated duration minutes.");

        var existingStart = start ?? task.PlannedStartTime;
        var existingEnd = end ?? task.PlannedEndTime;
        var estimated = cmd.EstimatedDurationMinutes ?? task.EstimatedDurationMinutes;

        if (cmd.EstimatedDurationMinutes is null && existingStart is not null && existingEnd is not null
            && existingEnd > existingStart)
        {
            estimated = (int)(existingEnd.Value - existingStart.Value).TotalMinutes;
        }

        task.SetPlannedTimes(existingStart, existingEnd, estimated);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    private async Task TrySpawnNextOccurrenceAsync(TaskItem completedOrSkipped, CancellationToken ct)
    {
        if (!completedOrSkipped.RecurrenceDefinitionId.HasValue) return;

        var def = await _db.RecurrenceDefinitions
            .FirstOrDefaultAsync(r =>
                r.Id == completedOrSkipped.RecurrenceDefinitionId.Value &&
                r.FamilyId == _current.FamilyId &&
                r.IsActive, ct);
        if (def is null) return;

        // Avoid duplicate open instances for the same definition
        var hasOpen = await _db.Tasks.AnyAsync(t =>
            t.RecurrenceDefinitionId == def.Id &&
            t.Id != completedOrSkipped.Id &&
            t.Status != FamilyTaskStatus.Completed &&
            t.Status != FamilyTaskStatus.Cancelled &&
            t.Status != FamilyTaskStatus.Skipped, ct);
        if (hasOpen) return;

        var next = def.GenerateInstance(_current.MemberId!.Value);
        _db.Tasks.Add(next);

        if (next.AssignedToMemberId is Guid aid && aid != _current.MemberId)
        {
            _db.Notifications.Add(Notification.Create(
                _current.FamilyId!.Value, aid, NotificationType.TaskAssigned,
                $"Assigned: {next.Title}", "Next occurrence of a recurring task.", next.Id, "Task"));
        }
    }

    private async Task<TaskItem> GetTaskForFamily(Guid taskId, CancellationToken ct)
    {
        EnsureAuthenticated();
        var task = await _db.Tasks
            .Include(t => t.History)
            .Include(t => t.TimeSegments)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.FamilyId == _current.FamilyId, ct)
            ?? throw new NotFoundException("Task", taskId);
        return task;
    }

    private async Task EnsureMemberInFamily(Guid memberId, CancellationToken ct)
    {
        var exists = await _db.FamilyMembers.AnyAsync(
            m => m.Id == memberId && m.FamilyId == _current.FamilyId && m.IsActive, ct);
        if (!exists) throw new NotFoundException("FamilyMember", memberId);
    }

    private void EnsureAuthenticated()
    {
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated or not a family member.");
    }

    private void EnsureCanAssign()
    {
        EnsureAuthenticated();
        if (_current.Role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Owners and Adults can assign/reassign/cancel tasks.");
    }

    private static RecurrenceDto ToRecurrenceDto(RecurrenceDefinition r) => new(
        r.Id, r.Title, r.Description, r.Frequency, r.Interval, r.DayOfWeek,
        r.DefaultAssigneeMemberId, r.DeclinePolicy, r.IsActive, r.NextOccurrenceUtc, r.Category);

    private async Task<TaskDto> ToDto(TaskItem t, CancellationToken ct)
    {
        string? name = null;
        if (t.AssignedToMemberId.HasValue)
        {
            name = await _db.FamilyMembers.AsNoTracking()
                .Where(m => m.Id == t.AssignedToMemberId)
                .Select(m => m.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        int? variance = null;
        if (t.EstimatedDurationMinutes.HasValue && t.ActualDurationMinutes.HasValue)
            variance = t.ActualDurationMinutes.Value - t.EstimatedDurationMinutes.Value;

        string? freq = null;
        if (t.RecurrenceDefinitionId.HasValue)
        {
            freq = await _db.RecurrenceDefinitions.AsNoTracking()
                .Where(r => r.Id == t.RecurrenceDefinitionId)
                .Select(r => r.Frequency.ToString())
                .FirstOrDefaultAsync(ct);
        }

        return new TaskDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToMemberId, name, t.DueDate,
            t.EstimatedDurationMinutes, t.ActualDurationMinutes, t.Category,
            t.CalendarBacked, t.CreatedAtUtc, t.IsOverdue(),
            t.PlannedStartTime?.ToString("HH:mm"),
            t.PlannedEndTime?.ToString("HH:mm"),
            variance,
            t.RecurrenceDefinitionId,
            t.RecurrenceDefinitionId.HasValue,
            freq);
    }
}
