using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using FamilyOS.Domain.Procurement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Pulse;

public record OutcomesDto(
    int TasksCompletedLast7Days,
    int TasksCompletedLast30Days,
    int RequestsApprovedLast7Days,
    int RequestsDeniedLast7Days,
    int RequestsApprovedLast30Days,
    int RequestsDeniedLast30Days,
    int ShoppingItemsPurchasedLast7Days,
    double? AvgTaskCompletionMinutes,
    double? ApprovalRateLast7Days,
    int OpenTasks,
    int PendingRequests,
    int QueuedShoppingItems,
    IReadOnlyList<int> WeeklyTasksCompleted,
    IReadOnlyList<int> WeeklyRequestsApproved,
    IReadOnlyList<int> WeeklyShoppingPurchased);

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
            t.Status == FamilyTaskStatus.Completed
            && t.UpdatedAtUtc is { } c && c >= d7);
        var completed30 = tasks.Count(t =>
            t.Status == FamilyTaskStatus.Completed
            && t.UpdatedAtUtc is { } c && c >= d30);
        var open = tasks.Count(t =>
            t.Status is FamilyTaskStatus.Assigned or FamilyTaskStatus.Accepted
                or FamilyTaskStatus.InProgress or FamilyTaskStatus.Paused
                or FamilyTaskStatus.Created or FamilyTaskStatus.Reassigned);

        var durations = tasks
            .Where(t => t.Status == FamilyTaskStatus.Completed && t.ActualDurationMinutes is > 0)
            .Select(t => (double)t.ActualDurationMinutes!)
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
        var approved30 = requests.Count(r =>
            r.Status == RequestStatus.Approved
            && r.UpdatedAtUtc is { } u && u >= d30);
        var denied30 = requests.Count(r =>
            r.Status == RequestStatus.Denied
            && r.UpdatedAtUtc is { } u && u >= d30);
        var pending = requests.Count(r =>
            r.Status is RequestStatus.Submitted or RequestStatus.UnderReview
                or RequestStatus.WaitingForInformation or RequestStatus.ConditionallyApproved);

        double? approvalRate7 = null;
        var decided7 = approved7 + denied7;
        if (decided7 > 0)
            approvalRate7 = Math.Round(100.0 * approved7 / decided7, 1);

        var shop = await _db.ProcurementItems.AsNoTracking()
            .Where(i => i.FamilyId == familyId)
            .ToListAsync(ct);
        var purchased7 = shop.Count(i =>
            i.Status == ProcurementItemStatus.Purchased
            && i.UpdatedAtUtc is { } u && u >= d7);
        var queued = shop.Count(i =>
            i.Status is ProcurementItemStatus.Queued or ProcurementItemStatus.InCart
                or ProcurementItemStatus.Approved);

        // 8 week buckets ending this week (oldest → newest)
        var weeklyTasks = new int[8];
        var weeklyApproved = new int[8];
        var weeklyShop = new int[8];
        for (var w = 0; w < 8; w++)
        {
            var weekEnd = now.Date.AddDays(-7 * (7 - w));
            var weekStart = weekEnd.AddDays(-7);
            weeklyTasks[w] = tasks.Count(t =>
                t.Status == FamilyTaskStatus.Completed
                && t.UpdatedAtUtc is { } c && c >= weekStart && c < weekEnd);
            weeklyApproved[w] = requests.Count(r =>
                r.Status == RequestStatus.Approved
                && r.UpdatedAtUtc is { } u && u >= weekStart && u < weekEnd);
            weeklyShop[w] = shop.Count(i =>
                i.Status == ProcurementItemStatus.Purchased
                && i.UpdatedAtUtc is { } u && u >= weekStart && u < weekEnd);
        }

        return new OutcomesDto(
            completed7,
            completed30,
            approved7,
            denied7,
            approved30,
            denied30,
            purchased7,
            avg,
            approvalRate7,
            open,
            pending,
            queued,
            weeklyTasks,
            weeklyApproved,
            weeklyShop);
    }
}
