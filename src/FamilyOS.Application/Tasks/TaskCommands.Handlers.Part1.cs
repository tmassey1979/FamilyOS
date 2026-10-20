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

public partial class TaskCommandHandlers :
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
            cmd.Description, cmd.Priority, cmd.DueDate ?? recurrence?.NextOccurrenceUtc, cmd.Category,
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
            if (instance.AssigneeMemberId is Guid aid && aid != _current.MemberId)
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

}
