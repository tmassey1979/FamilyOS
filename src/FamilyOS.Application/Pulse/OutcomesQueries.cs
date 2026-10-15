using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Procurement;
using FamilyOS.Domain.Requests;
using FamilyOS.Domain.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Pulse;

public record OutcomesDto(
    int TasksCompletedLast7Days,
    int TasksCompletedLast30Days,
    int RequestsApprovedLast7Days,
    int RequestsDeniedLast7Days,
    int ShoppingItemsPurchasedLast7Days,
    double? AvgTaskCompletionMinutes,
    int OpenTasks,
    int PendingRequests,
    int QueuedShoppingItems);

public record GetOutcomesQuery() : IRequest<OutcomesDto>;

public class OutcomesQueryHandler : IRequestHandler<GetOutcomesQuery, OutcomesDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public OutcomesQueryHandler(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<OutcomesDto> Handle(GetOutcomesQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var now = DateTime.UtcNow;
        var d7 = now.AddDays(-7);
        var d30 = now.AddDays(-30);

        var tasks = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId)
            .ToListAsync(ct);

        var completed7 = tasks.Count(t =>
            t.Status == TaskStatus.Completed
            && t.CompletedAtUtc is { } c && c >= d7);
        var completed30 = tasks.Count(t =>
            t.Status == TaskStatus.Completed
            && t.CompletedAtUtc is { } c && c >= d30);
        var open = tasks.Count(t =>
            t.Status is TaskStatus.Assigned or TaskStatus.Accepted or TaskStatus.InProgress);

        var durations = tasks
            .Where(t => t.Status == TaskStatus.Completed && t.CompletedAtUtc.HasValue && t.StartedAtUtc.HasValue)
            .Select(t => (t.CompletedAtUtc!.Value - t.StartedAtUtc!.Value).TotalMinutes)
            .Where(m => m is > 0 and < 24 * 60)
            .ToList();
        double? avg = durations.Count > 0 ? durations.Average() : null;

        var requests = await _db.Requests.AsNoTracking()
            .Where(r => r.FamilyId == familyId)
            .ToListAsync(ct);

        var approved7 = requests.Count(r =>
            r.Status == RequestStatus.Approved
            && r.UpdatedAtUtc is { } u && u >= d7);
        var denied7 = requests.Count(r =>
            r.Status == RequestStatus.Denied
            && r.UpdatedAtUtc is { } u && u >= d7);
        var pending = requests.Count(r =>
            r.Status is RequestStatus.Submitted or RequestStatus.PendingApproval or RequestStatus.Conditional);

        var shop = await _db.ProcurementItems.AsNoTracking()
            .Where(i => i.FamilyId == familyId)
            .ToListAsync(ct);
        var purchased7 = shop.Count(i =>
            i.Status == ProcurementItemStatus.Purchased
            && i.UpdatedAtUtc is { } u && u >= d7);
        var queued = shop.Count(i =>
            i.Status is ProcurementItemStatus.Queued or ProcurementItemStatus.InCart);

        return new OutcomesDto(
            completed7,
            completed30,
            approved7,
            denied7,
            purchased7,
            avg,
            open,
            pending,
            queued);
    }
}
