using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Tasks;
using FamilyOS.Domain.TimeIntelligence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Tasks;

public record TaskDto(
    Guid Id, string Title, string? Description, FamilyTaskStatus Status, TaskPriority Priority,
    Guid? AssignedToMemberId, string? AssignedToName, DateTime? DueDate,
    int? EstimatedDurationMinutes, int? ActualDurationMinutes, string? Category,
    bool CalendarBacked, DateTime CreatedAtUtc);

public record TaskHistoryDto(Guid Id, FamilyTaskStatus Status, Guid ActorMemberId, string Detail, DateTime TimestampUtc);

public record CreateTaskCommand(
    string Title, string? Description, Guid? AssignToMemberId, TaskPriority Priority,
    DateTime? DueDate, string? Category, bool CalendarBacked, int? EstimatedDurationMinutes
) : IRequest<TaskDto>;

public record AssignTaskCommand(Guid TaskId, Guid AssignToMemberId) : IRequest<TaskDto>;
public record AcceptTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeclineTaskCommand(Guid TaskId, Guid? ReasonId, string? Note) : IRequest<TaskDto>;
public record StartTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record PauseTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record ResumeTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record CompleteTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record DeferTaskCommand(Guid TaskId, string? Note) : IRequest<TaskDto>;
public record CancelTaskCommand(Guid TaskId, string? Reason) : IRequest<TaskDto>;
public record ReassignTaskCommand(Guid TaskId, Guid NewAssigneeMemberId) : IRequest<TaskDto>;
public record GetTaskQuery(Guid TaskId) : IRequest<TaskDto>;
public record GetMyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetFamilyTasksQuery(FamilyTaskStatus? Status = null) : IRequest<List<TaskDto>>;
public record GetTaskHistoryQuery(Guid TaskId) : IRequest<List<TaskHistoryDto>>;

public class TaskCommandHandlers :
    IRequestHandler<CreateTaskCommand, TaskDto>,
    IRequestHandler<AssignTaskCommand, TaskDto>,
    IRequestHandler<AcceptTaskCommand, TaskDto>,
    IRequestHandler<DeclineTaskCommand, TaskDto>,
    IRequestHandler<StartTaskCommand, TaskDto>,
    IRequestHandler<PauseTaskCommand, TaskDto>,
    IRequestHandler<ResumeTaskCommand, TaskDto>,
    IRequestHandler<CompleteTaskCommand, TaskDto>,
    IRequestHandler<DeferTaskCommand, TaskDto>,
    IRequestHandler<CancelTaskCommand, TaskDto>,
    IRequestHandler<ReassignTaskCommand, TaskDto>,
    IRequestHandler<GetTaskQuery, TaskDto>,
    IRequestHandler<GetMyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetFamilyTasksQuery, List<TaskDto>>,
    IRequestHandler<GetTaskHistoryQuery, List<TaskHistoryDto>>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public TaskCommandHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<TaskDto> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureAuthenticated();

        var task = TaskItem.Create(
            _current.FamilyId!.Value, cmd.Title, _current.MemberId!.Value,
            cmd.Description, cmd.Priority, cmd.DueDate, cmd.Category, cmd.CalendarBacked);

        if (cmd.EstimatedDurationMinutes.HasValue)
            task.SetPlannedTimes(null, null, cmd.EstimatedDurationMinutes);

        if (cmd.AssignToMemberId.HasValue)
        {
            await EnsureMemberInFamily(cmd.AssignToMemberId.Value, ct);
            task.Assign(cmd.AssignToMemberId.Value, _current.MemberId!.Value);
        }

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return await ToDto(task, ct);
    }

    public async Task<TaskDto> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        EnsureCanAssign();
        var task = await GetTaskForFamily(cmd.TaskId, ct);
        await EnsureMemberInFamily(cmd.AssignToMemberId, ct);
        task.Assign(cmd.AssignToMemberId, _current.MemberId!.Value);
        await _db.SaveChangesAsync(ct);
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
        await _db.SaveChangesAsync(ct);
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

        return new TaskDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToMemberId, name, t.DueDate,
            t.EstimatedDurationMinutes, t.ActualDurationMinutes, t.Category,
            t.CalendarBacked, t.CreatedAtUtc);
    }
}
