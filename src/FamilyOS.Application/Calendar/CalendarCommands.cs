using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Calendar;
using FamilyOS.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FamilyOS.Application.Calendar;

public record CreateCalendarEventCommand(
    string Title,
    DateTime StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Location,
    string? Description,
    Guid? LinkedTaskId,
    Guid? LinkedRequestId) : IRequest<CalendarEventDto>;

public record UpdateCalendarEventCommand(
    Guid EventId,
    string Title,
    DateTime StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Location) : IRequest<CalendarEventDto>;

public record CancelCalendarEventCommand(Guid EventId) : IRequest;

public class CalendarCommandHandlers :
    IRequestHandler<CreateCalendarEventCommand, CalendarEventDto>,
    IRequestHandler<UpdateCalendarEventCommand, CalendarEventDto>,
    IRequestHandler<CancelCalendarEventCommand>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IGoogleCalendarClient _google;

    public CalendarCommandHandlers(
        IFamilyOsDbContext db,
        ICurrentUserService current,
        IGoogleCalendarClient google)
    {
        _db = db;
        _current = current;
        _google = google;
    }

    public async Task<CalendarEventDto> Handle(CreateCalendarEventCommand cmd, CancellationToken ct)
    {
        var (familyId, memberId, _) = await _current.RequireFamilyAsync(ct);
        if (string.IsNullOrWhiteSpace(cmd.Title))
            throw new DomainException("Title required.");

        var ev = CalendarEvent.Create(
            familyId,
            cmd.Title,
            cmd.StartUtc,
            memberId,
            cmd.EndUtc,
            cmd.AllDay,
            cmd.Location,
            cmd.LinkedTaskId,
            cmd.LinkedRequestId,
            cmd.Description);

        _db.CalendarEvents.Add(ev);
        await _db.SaveChangesAsync(ct);

        // Best-effort push to the creator's Google Calendar when connected.
        await GoogleCalendarSync.TryPushEventAsync(_db, _google, ev, ct);

        return ToDto(ev);
    }

    public async Task<CalendarEventDto> Handle(UpdateCalendarEventCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is FamilyRole.Child)
            throw new ForbiddenException("Children cannot edit calendar events.");

        var ev = await _db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == cmd.EventId && e.FamilyId == familyId, ct)
            ?? throw new NotFoundException("CalendarEvent", cmd.EventId);

        if (ev.IsCancelled)
            throw new DomainException("Event is cancelled.");

        ev.Update(cmd.Title, cmd.StartUtc, cmd.EndUtc, cmd.AllDay, cmd.Location);
        await _db.SaveChangesAsync(ct);

        // Best-effort update on Google when linked
        if (!string.IsNullOrWhiteSpace(ev.ExternalEventId)
            && string.Equals(ev.ExternalProvider, GoogleCalendarSync.Provider, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var conn = await _db.GoogleCalendarConnections
                    .FirstOrDefaultAsync(c => c.MemberId == ev.CreatedByMemberId && c.IsActive, ct);
                if (conn is not null)
                {
                    await GoogleCalendarSync.EnsureValidAccessTokenAsync(_db, _google, conn, ct);
                    await _google.UpdateEventAsync(
                        conn.AccessToken,
                        ev.ExternalCalendarId ?? conn.CalendarId,
                        ev.ExternalEventId!,
                        new GoogleEventInput(ev.Title, ev.StartUtc, ev.EndUtc, ev.AllDay, ev.Location, ev.Description),
                        ct);
                }
            }
            catch { /* best-effort */ }
        }

        return ToDto(ev);
    }

    public async Task Handle(CancelCalendarEventCommand cmd, CancellationToken ct)
    {
        var (familyId, _, role) = await _current.RequireFamilyAsync(ct);
        if (role is FamilyRole.Child)
            throw new ForbiddenException("Children cannot cancel calendar events.");

        var ev = await _db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == cmd.EventId && e.FamilyId == familyId, ct)
            ?? throw new NotFoundException("CalendarEvent", cmd.EventId);

        ev.Cancel();
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(ev.ExternalEventId)
            && string.Equals(ev.ExternalProvider, GoogleCalendarSync.Provider, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var conn = await _db.GoogleCalendarConnections
                    .FirstOrDefaultAsync(c => c.MemberId == ev.CreatedByMemberId && c.IsActive, ct);
                if (conn is not null)
                {
                    await GoogleCalendarSync.EnsureValidAccessTokenAsync(_db, _google, conn, ct);
                    await _google.DeleteEventAsync(
                        conn.AccessToken,
                        ev.ExternalCalendarId ?? conn.CalendarId,
                        ev.ExternalEventId!,
                        ct);
                }
            }
            catch { /* best-effort */ }
        }
    }

    private static CalendarEventDto ToDto(CalendarEvent e) =>
        new(e.Id, e.Title, e.Description, e.StartUtc, e.EndUtc, e.AllDay, e.Location,
            e.LinkedTaskId, e.LinkedRequestId);
}
