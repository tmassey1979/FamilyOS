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

        var actualMinutes = task.TimeSegments
            .Where(s => s.EndedAtUtc.HasValue)
            .Sum(s => s.DurationMinutes);
        if (actualMinutes > 0)
        {
            var category = task.Category ?? "General";
            _db.TaskTimingRecords.Add(TaskTimingRecord.Create(
                _current.FamilyId!.Value, category, actualMinutes,
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
        var newDue = DateTime.UtcNow.Date.AddDays(1);
        task.Defer(_current.MemberId!.Value, newDue, cmd.Note);
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
        var isAssignee = task.AssigneeMemberId == _current.MemberId;
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
        var q = _db.Tasks.AsNoTracking().Where(t => t.FamilyId == _current.FamilyId && t.AssigneeMemberId == _current.MemberId);
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
        task.UpdateDetails(cmd.Title, cmd.Description, cmd.Category);
        if (cmd.Priority.HasValue)
            task.SetPriority(cmd.Priority.Value);
        if (cmd.ClearDueDate)
            task.SetDueDate(null);
        else if (cmd.DueDate.HasValue)
            task.SetDueDate(cmd.DueDate);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(SetPlannedTimesCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        static DateTime? ParseTime(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (TimeOnly.TryParse(s, out var to))
                return DateTime.UtcNow.Date.Add(to.ToTimeSpan());
            if (DateTime.TryParse(s, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }
        task.SetPlannedTimes(ParseTime(cmd.PlannedStartTime), ParseTime(cmd.PlannedEndTime), cmd.EstimatedDurationMinutes);
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
        if (next.AssigneeMemberId is Guid aid && aid != _current.MemberId)
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
        if (t.AssigneeMemberId is Guid aid)
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

        var actual = t.TimeSegments.Where(s => s.EndedAtUtc.HasValue).Sum(s => s.DurationMinutes);
        int? actualOrNull = actual > 0 ? actual : null;
        int? variance = null;
        if (t.EstimatedMinutes is int est && actualOrNull is int act)
            variance = act - est;
        string? plannedStart = t.PlannedStartUtc?.ToString("HH:mm");
        string? plannedEnd = t.PlannedEndUtc?.ToString("HH:mm");

        return new TaskDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority,
            t.AssigneeMemberId, name, t.DueDate,
            t.EstimatedMinutes, actualOrNull, t.Category,
            t.CalendarBacked, t.CreatedAtUtc, t.IsOverdue(),
            plannedStart, plannedEnd, variance,
            t.RecurrenceDefinitionId, isRecurring, freq);
    }
}
