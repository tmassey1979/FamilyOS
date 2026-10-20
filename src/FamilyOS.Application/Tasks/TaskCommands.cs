using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
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

        _db.Tasks.Add(task);

        if (recurrence is not null)
        {
            var from = (task.DueDate ?? DateTime.UtcNow).Date;
            AdvanceDefinition(recurrence, from);
        }

        await _db.SaveChangesAsync(ct);
        if (cmd.AssignToMemberId.HasValue && cmd.AssignToMemberId.Value != _current.MemberId)
            await TryPushAsync(cmd.AssignToMemberId.Value, $"Assigned: {task.Title}", "You have a new task to accept.", task.Id, "Task", ct);
        return await ToDto(task, ct);
    }

    private static void AdvanceDefinition(RecurrenceDefinition def, DateTime from)
    {
        // Placeholder: domain advances via GenerateInstance on complete/skip.
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
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(GetTaskQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(query.TaskId, ct);
        return await ToDto(task, ct);
    }

    public async Task<List<TaskDto>> Handle(GetMyTasksQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var q = _db.Tasks.AsNoTracking().Where(t => t.FamilyId == _current.FamilyId && t.AssignedToMemberId == _current.MemberId);
        if (query.Status.HasValue) q = q.Where(t => t.Status == query.Status);
        var list = await q.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var result = new List<TaskDto>();
        foreach (var t in list) result.Add(await ToDto(t, ct));
        return result;
    }

    public async Task<List<TaskDto>> Handle(GetFamilyTasksQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var q = _db.Tasks.AsNoTracking().Where(t => t.FamilyId == _current.FamilyId);
        if (query.Status.HasValue) q = q.Where(t => t.Status == query.Status);
        var list = await q.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var result = new List<TaskDto>();
        foreach (var t in list) result.Add(await ToDto(t, ct));
        return result;
    }

    public async Task<List<TaskHistoryDto>> Handle(GetTaskHistoryQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(query.TaskId, ct);
        return task.History.OrderBy(h => h.TimestampUtc)
            .Select(h => new TaskHistoryDto(h.Id, h.Status, h.ActorMemberId, h.Detail, h.TimestampUtc))
            .ToList();
    }

    public async Task<TaskDto> Handle(UpdateTaskDetailsCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.UpdateDetails(cmd.Title, cmd.Description, cmd.Priority, cmd.Category, cmd.DueDate, cmd.ClearDueDate);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(SetPlannedTimesCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        task.SetPlannedTimes(cmd.PlannedStartTime, cmd.PlannedEndTime, cmd.EstimatedDurationMinutes);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    private async Task TrySpawnNextOccurrenceAsync(TaskItem task, CancellationToken ct)
    {
        if (task.RecurrenceDefinitionId is not Guid rid) return;
        var def = await _db.RecurrenceDefinitions.FirstOrDefaultAsync(r => r.Id == rid && r.FamilyId == _current.FamilyId, ct);
        if (def is null || !def.IsActive) return;
        var next = def.GenerateInstance(_current.MemberId!.Value);
        _db.Tasks.Add(next);
        if (next.AssignedToMemberId is Guid aid && aid != _current.MemberId)
        {
            _db.Notifications.Add(Notification.Create(
                _current.FamilyId!.Value, aid, NotificationType.TaskAssigned,
                $"Assigned: {next.Title}", "Next occurrence of a recurring task.", next.Id, "Task"));
        }
    }

    private async Task<TaskItem> GetTaskForFamily(Guid taskId, CancellationToken ct) =>
        await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.FamilyId == _current.FamilyId, ct)
        ?? throw new NotFoundException("Task", taskId);

    private async Task EnsureMemberInFamily(Guid memberId, CancellationToken ct)
    {
        var ok = await _db.FamilyMembers.AnyAsync(m => m.Id == memberId && m.FamilyId == _current.FamilyId && m.IsActive, ct);
        if (!ok) throw new NotFoundException("FamilyMember", memberId);
    }

    private void EnsureAuthenticated()
    {
        if (_current.MemberId is null || _current.FamilyId is null)
            throw new UnauthorizedException("Not authenticated.");
    }

    private void EnsureCanAssign()
    {
        EnsureAuthenticated();
        if (_current.Role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults/Owners can assign tasks.");
    }

    private static RecurrenceDto ToRecurrenceDto(RecurrenceDefinition d) =>
        new(d.Id, d.Title, d.Description, d.Frequency, d.Interval, d.DayOfWeek,
            d.DefaultAssigneeMemberId, d.DeclinePolicy, d.IsActive, d.NextOccurrenceUtc, d.Category);

    private async Task<TaskDto> ToDto(TaskItem t, CancellationToken ct)
    {
        string? name = null;
        if (t.AssignedToMemberId is Guid aid)
        {
            name = await _db.FamilyMembers.AsNoTracking()
                .Where(m => m.Id == aid)
                .Select(m => m.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        string? freq = null;
        bool isRecurring = t.RecurrenceDefinitionId.HasValue;
        if (t.RecurrenceDefinitionId is Guid rid)
        {
            var f = await _db.RecurrenceDefinitions.AsNoTracking()
                .Where(r => r.Id == rid)
                .Select(r => (RecurrenceFrequency?)r.Frequency)
                .FirstOrDefaultAsync(ct);
            freq = f?.ToString();
        }

        return new TaskDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToMemberId, name, t.DueDate,
            t.EstimatedDurationMinutes, t.ActualDurationMinutes, t.Category,
            t.CalendarBacked, t.CreatedAtUtc, t.IsOverdue,
            t.PlannedStartTime, t.PlannedEndTime, t.VarianceMinutes,
            t.RecurrenceDefinitionId, isRecurring, freq);
    }
}
