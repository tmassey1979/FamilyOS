using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Conditions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Conditions;

public record ConditionDto(
    Guid Id,
    Guid? RequestId,
    Guid? TaskId,
    string Name,
    string? Description,
    ConditionType Type,
    ConditionStatus Status,
    ConditionLogic LogicGroup,
    int GroupOrder,
    DateTime? DeadlineUtc,
    Guid? SatisfiedByMemberId,
    DateTime? SatisfiedAtUtc);

public record GetConditionsForRequestQuery(Guid RequestId) : IRequest<List<ConditionDto>>;
public record GetPendingConditionsQuery() : IRequest<List<ConditionDto>>;

public record CreateConditionCommand(
    string Name,
    ConditionType Type,
    Guid? RequestId,
    Guid? TaskId,
    string? Description,
    DateTime? DeadlineUtc,
    ConditionLogic LogicGroup,
    int GroupOrder) : IRequest<ConditionDto>;

public record SatisfyConditionCommand(Guid ConditionId) : IRequest<ConditionDto>;
public record FailConditionCommand(Guid ConditionId, string? Reason) : IRequest<ConditionDto>;
public record CancelConditionCommand(Guid ConditionId) : IRequest<ConditionDto>;

public class ConditionHandlers :
    IRequestHandler<GetConditionsForRequestQuery, List<ConditionDto>>,
    IRequestHandler<GetPendingConditionsQuery, List<ConditionDto>>,
    IRequestHandler<CreateConditionCommand, ConditionDto>,
    IRequestHandler<SatisfyConditionCommand, ConditionDto>,
    IRequestHandler<FailConditionCommand, ConditionDto>,
    IRequestHandler<CancelConditionCommand, ConditionDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public ConditionHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<ConditionDto>> Handle(GetConditionsForRequestQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var list = await _db.Conditions.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.RequestId == query.RequestId)
            .OrderBy(c => c.GroupOrder).ThenBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<List<ConditionDto>> Handle(GetPendingConditionsQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var list = await _db.Conditions.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.Status == ConditionStatus.Pending)
            .OrderBy(c => c.DeadlineUtc).ThenBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ConditionDto> Handle(CreateConditionCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults/Owners can create conditions.");

        if (cmd.RequestId.HasValue)
        {
            var exists = await _db.Requests.AnyAsync(
                r => r.Id == cmd.RequestId && r.FamilyId == familyId, ct);
            if (!exists) throw new NotFoundException("Request", cmd.RequestId.Value);
        }

        var condition = Condition.Create(
            familyId,
            cmd.Name,
            cmd.Type,
            cmd.RequestId,
            cmd.TaskId,
            cmd.Description,
            cmd.DeadlineUtc,
            cmd.LogicGroup,
            cmd.GroupOrder);

        _db.Conditions.Add(condition);
        await _db.SaveChangesAsync(ct);
        return ToDto(condition);
    }

    public async Task<ConditionDto> Handle(SatisfyConditionCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        var condition = await GetOwned(cmd.ConditionId, familyId, ct);
        condition.Satisfy(memberId);
        await _db.SaveChangesAsync(ct);
        return ToDto(condition);
    }

    public async Task<ConditionDto> Handle(FailConditionCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults/Owners can fail conditions.");

        var condition = await GetOwned(cmd.ConditionId, familyId, ct);
        condition.Fail(cmd.Reason);
        await _db.SaveChangesAsync(ct);
        return ToDto(condition);
    }

    public async Task<ConditionDto> Handle(CancelConditionCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is not (FamilyRole.Owner or FamilyRole.Adult))
            throw new ForbiddenException("Only Adults/Owners can cancel conditions.");

        var condition = await GetOwned(cmd.ConditionId, familyId, ct);
        condition.Cancel();
        await _db.SaveChangesAsync(ct);
        return ToDto(condition);
    }

    private async Task<Condition> GetOwned(Guid id, Guid familyId, CancellationToken ct) =>
        await _db.Conditions.FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId, ct)
        ?? throw new NotFoundException("Condition", id);

    private static ConditionDto ToDto(Condition c) =>
        new(c.Id, c.RequestId, c.TaskId, c.Name, c.Description, c.Type, c.Status,
            c.LogicGroup, c.GroupOrder, c.DeadlineUtc, c.SatisfiedByMemberId, c.SatisfiedAtUtc);

}
