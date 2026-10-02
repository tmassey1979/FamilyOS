using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Calendar;

public record CalendarEventDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Location,
    Guid? LinkedTaskId,
    Guid? LinkedRequestId);

public record GetUpcomingEventsQuery(int DaysAhead = 14) : IRequest<List<CalendarEventDto>>;
public record GetEventsRangeQuery(DateTime FromUtc, DateTime ToUtc) : IRequest<List<CalendarEventDto>>;

public class CalendarQueryHandlers :
    IRequestHandler<GetUpcomingEventsQuery, List<CalendarEventDto>>,
    IRequestHandler<GetEventsRangeQuery, List<CalendarEventDto>>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;

    public CalendarQueryHandlers(IFamilyOsDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<CalendarEventDto>> Handle(GetUpcomingEventsQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);
        var from = DateTime.UtcNow.AddHours(-1);
        var to = DateTime.UtcNow.AddDays(Math.Clamp(query.DaysAhead, 1, 90));

        return await _db.CalendarEvents.AsNoTracking()
            .Where(e => e.FamilyId == familyId && !e.IsCancelled && e.StartUtc >= from && e.StartUtc <= to)
            .OrderBy(e => e.StartUtc)
            .Select(e => new CalendarEventDto(
                e.Id, e.Title, e.Description, e.StartUtc, e.EndUtc, e.AllDay, e.Location,
                e.LinkedTaskId, e.LinkedRequestId))
            .ToListAsync(ct);
    }

    public async Task<List<CalendarEventDto>> Handle(GetEventsRangeQuery query, CancellationToken ct)
    {
        var (familyId, _, _) = await _current.RequireFamilyAsync(ct);

        return await _db.CalendarEvents.AsNoTracking()
            .Where(e => e.FamilyId == familyId && !e.IsCancelled
                        && e.StartUtc < query.ToUtc
                        && (e.EndUtc == null || e.EndUtc > query.FromUtc))
            .OrderBy(e => e.StartUtc)
            .Select(e => new CalendarEventDto(
                e.Id, e.Title, e.Description, e.StartUtc, e.EndUtc, e.AllDay, e.Location,
                e.LinkedTaskId, e.LinkedRequestId))
            .ToListAsync(ct);
    }
}
