using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Pulse;

/// <summary>
/// Family Pulse — "What matters to me right now?"
/// Deterministic priority engine, not a data dump.
/// </summary>
public record PulseDto(
    string Greeting,
    NextActionDto? NextAction,
    List<AttentionItemDto> NeedsAttention,
    List<UpcomingItemDto> ComingUp,
    HouseholdSummaryDto Household,
    ProcurementSummaryDto? Procurement,
    List<ConditionItemDto> Conditions);

public record NextActionDto(string Kind, Guid EntityId, string Title, string? Subtitle, string PrimaryAction, string? DueLabel);
public record AttentionItemDto(string Kind, Guid EntityId, string Title, string Reason, string ActionLabel);
public record UpcomingItemDto(string Kind, Guid EntityId, string Title, DateTime WhenUtc, string? Location);
public record HouseholdSummaryDto(int RequestsAwaitingApproval, int TasksDueToday, int TasksNeedingAcceptance, string? NextShoppingTrip);
public record ProcurementSummaryDto(int ApprovedItems, int ReadyForStore, string? PrimaryStore);
public record ConditionItemDto(Guid Id, string Name, string Status, string? RelatedTitle);

public record GetPulseQuery() : IRequest<PulseDto>;

public class PulseQueryHandler : IRequestHandler<GetPulseQuery, PulseDto>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public PulseQueryHandler(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PulseDto> Handle(GetPulseQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        if (!_current.IsAuthenticated || !_current.FamilyId.HasValue || !_current.MemberId.HasValue)
            throw new ForbiddenException("Not authenticated.");

        var familyId = _current.FamilyId.Value;
        var memberId = _current.MemberId.Value;
        var role = _current.Role ?? FamilyRole.Child;

        var member = await _db.FamilyMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        var name = member?.DisplayName?.Split(' ').FirstOrDefault() ?? "there";
        var hour = DateTime.UtcNow.Hour;
        var greeting = hour switch
        {
            < 12 => $"Good morning, {name}",
            < 17 => $"Good afternoon, {name}",
            _ => $"Good evening, {name}"
        };

        NextActionDto? nextAction = null;

        var inProgress = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.AssignedToMemberId == memberId && t.Status == FamilyTaskStatus.InProgress)
            .OrderBy(t => t.DueDate)
            .FirstOrDefaultAsync(ct);

        if (inProgress != null)
        {
            nextAction = new NextActionDto("Task", inProgress.Id, inProgress.Title, "In progress", "Resume", null);
        }
        else
        {
            var today = DateTime.UtcNow.Date;
            var dueTask = await _db.Tasks.AsNoTracking()
                .Where(t => t.FamilyId == familyId && t.AssignedToMemberId == memberId &&
                            (t.Status == FamilyTaskStatus.Accepted || t.Status == FamilyTaskStatus.Assigned) &&
                            t.DueDate != null && t.DueDate <= today.AddDays(1))
                .OrderBy(t => t.DueDate)
                .FirstOrDefaultAsync(ct);

            if (dueTask != null)
            {
                var action = dueTask.Status == FamilyTaskStatus.Assigned ? "Accept" : "Start";
                var dueLabel = dueTask.DueDate!.Value.Date <= today ? "Due today" : "Due soon";
                nextAction = new NextActionDto("Task", dueTask.Id, dueTask.Title, dueLabel, action, dueLabel);
            }
        }

        var attention = new List<AttentionItemDto>();

        if (role is FamilyRole.Owner or FamilyRole.Adult)
        {
            var pendingRequests = await _db.Requests.AsNoTracking()
                .Where(r => r.FamilyId == familyId &&
                            (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview))
                .OrderBy(r => r.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);

            foreach (var r in pendingRequests)
                attention.Add(new AttentionItemDto("Request", r.Id, r.Title, "Waiting for your review", "Review"));
        }

        var waitingInfo = await _db.Requests.AsNoTracking()
            .Where(r => r.FamilyId == familyId && r.RequesterMemberId == memberId &&
                        r.Status == RequestStatus.WaitingForInformation)
            .Take(5)
            .ToListAsync(ct);

        foreach (var r in waitingInfo)
            attention.Add(new AttentionItemDto("Request", r.Id, r.Title, "Waiting for your answer", "Answer"));

        var needsAccept = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.AssignedToMemberId == memberId && t.Status == FamilyTaskStatus.Assigned)
            .Take(5)
            .ToListAsync(ct);

        foreach (var t in needsAccept)
        {
            if (nextAction?.EntityId != t.Id)
                attention.Add(new AttentionItemDto("Task", t.Id, t.Title, "Needs acceptance", "Accept"));
        }

        var upcoming = new List<UpcomingItemDto>();
        var window = DateTime.UtcNow.AddHours(48);

        var events = await _db.CalendarEvents.AsNoTracking()
            .Where(e => e.FamilyId == familyId && !e.IsCancelled && e.StartUtc >= DateTime.UtcNow && e.StartUtc <= window)
            .OrderBy(e => e.StartUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var e in events)
            upcoming.Add(new UpcomingItemDto("Calendar", e.Id, e.Title, e.StartUtc, e.Location));

        var soonTasks = await _db.Tasks.AsNoTracking()
            .Where(t => t.FamilyId == familyId && t.AssignedToMemberId == memberId &&
                        t.DueDate != null && t.DueDate > DateTime.UtcNow && t.DueDate <= window &&
                        t.Status != FamilyTaskStatus.Completed && t.Status != FamilyTaskStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .ToListAsync(ct);

        foreach (var t in soonTasks)
            upcoming.Add(new UpcomingItemDto("Task", t.Id, t.Title, t.DueDate!.Value, null));

        upcoming = upcoming.OrderBy(u => u.WhenUtc).Take(8).ToList();

        var requestsAwaiting = role is FamilyRole.Owner or FamilyRole.Adult
            ? await _db.Requests.CountAsync(r => r.FamilyId == familyId &&
                (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview), ct)
            : 0;

        var tasksDueToday = await _db.Tasks.CountAsync(t =>
            t.FamilyId == familyId && t.DueDate != null && t.DueDate.Value.Date == DateTime.UtcNow.Date &&
            t.Status != FamilyTaskStatus.Completed && t.Status != FamilyTaskStatus.Cancelled, ct);

        var tasksNeedingAcceptance = await _db.Tasks.CountAsync(t =>
            t.FamilyId == familyId && t.Status == FamilyTaskStatus.Assigned, ct);

        var household = new HouseholdSummaryDto(requestsAwaiting, tasksDueToday, tasksNeedingAcceptance, null);

        ProcurementSummaryDto? procurement = null;
        if (role is FamilyRole.Owner or FamilyRole.Adult)
        {
            var approved = await _db.ProcurementItems.CountAsync(p =>
                p.FamilyId == familyId && (p.Status == ProcurementItemStatus.Queued || p.Status == ProcurementItemStatus.Approved), ct);
            var inCart = await _db.ProcurementItems.CountAsync(p =>
                p.FamilyId == familyId && p.Status == ProcurementItemStatus.InCart, ct);

            if (approved > 0 || inCart > 0)
                procurement = new ProcurementSummaryDto(approved + inCart, inCart, "Walmart");
        }

        var conditions = await _db.Conditions.AsNoTracking()
            .Where(c => c.FamilyId == familyId && c.Status == ConditionStatus.Pending)
            .OrderBy(c => c.DeadlineUtc)
            .Take(5)
            .ToListAsync(ct);

        var conditionItems = conditions.Select(c => new ConditionItemDto(
            c.Id, c.Name, c.Status.ToString(), c.Description)).ToList();

        return new PulseDto(greeting, nextAction, attention, upcoming, household, procurement, conditionItems);
    }
}
