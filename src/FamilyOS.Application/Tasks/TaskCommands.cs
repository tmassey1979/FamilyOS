using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using FamilyOS.Domain.Notifications;
using FamilyOS.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Tasks;

// NOTE: Full TaskCommands restored after accidental empty push.
// Handlers for create/assign/complete/skip/etc. live below.

public record TaskDto(
    Guid Id,
    Guid FamilyId,
    string Title,
    string? Description,
    FamilyTaskStatus Status,
    TaskPriority Priority,
    Guid? AssigneeMemberId,
    DateTime? DueAtUtc,
    DateTime? PlannedStartUtc,
    DateTime? PlannedEndUtc,
    int? VarianceMinutes,
    bool IsOverdue,
    Guid? RecurrenceId,
    DateTime CreatedAtUtc);

public record CreateTaskCommand(
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeMemberId,
    DateTime? DueAtUtc,
    int? EstimatedMinutes) : IRequest<TaskDto>;

public record CompleteTaskCommand(Guid TaskId) : IRequest<TaskDto>;
public record AssignTaskCommand(Guid TaskId, Guid AssigneeMemberId) : IRequest<TaskDto>;
public record SkipTaskCommand(Guid TaskId) : IRequest<TaskDto>;

public class TaskCommandHandlers :
    IRequestHandler<CreateTaskCommand, TaskDto>,
    IRequestHandler<CompleteTaskCommand, TaskDto>,
    IRequestHandler<AssignTaskCommand, TaskDto>,
    IRequestHandler<SkipTaskCommand, TaskDto>
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
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var task = TaskItem.Create(familyId, cmd.Title, memberId, cmd.Description, cmd.Priority);
        if (cmd.DueAtUtc.HasValue) task.SetDueDate(cmd.DueAtUtc);
        if (cmd.AssigneeMemberId.HasValue)
            task.Assign(cmd.AssigneeMemberId.Value, memberId);
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<TaskDto> Handle(CompleteTaskCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var task = await GetOwned(cmd.TaskId, familyId, ct);
        task.Complete(memberId);

        // Auto-satisfy linked conditions when a task completes
        var linked = await _db.Conditions
            .Where(c => c.FamilyId == familyId && c.TaskId == task.Id && c.Status == ConditionStatus.Pending)
            .ToListAsync(ct);
        foreach (var c in linked)
        {
            if (c.Type == ConditionType.TaskCompletion)
                c.Satisfy(memberId);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<TaskDto> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var task = await GetOwned(cmd.TaskId, familyId, ct);
        task.Assign(cmd.AssigneeMemberId, memberId);
        await _db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<TaskDto> Handle(SkipTaskCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var task = await GetOwned(cmd.TaskId, familyId, ct);
        task.Skip(memberId);
        await _db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    private async Task<TaskItem> GetOwned(Guid id, Guid familyId, CancellationToken ct) =>
        await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.FamilyId == familyId, ct)
        ?? throw new NotFoundException("Task", id);

    private static TaskDto ToDto(TaskItem t) => new(
        t.Id, t.FamilyId, t.Title, t.Description, t.Status, t.Priority,
        t.AssigneeMemberId, t.DueAtUtc, t.PlannedStartUtc, t.PlannedEndUtc,
        t.VarianceMinutes, t.IsOverdue, t.RecurrenceId, t.CreatedAtUtc);
}
