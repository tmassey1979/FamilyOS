using FamilyOS.Application.Common;
using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Calendar;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FamilyOS.Application.Calendar;

public record GoogleCalendarStatusDto(
    bool Connected,
    string? Email,
    string? CalendarId,
    DateTime? LastSyncAtUtc,
    bool Configured);

public record GetGoogleCalendarAuthUrlQuery() : IRequest<string>;
public record GetGoogleCalendarStatusQuery() : IRequest<GoogleCalendarStatusDto>;
public record CompleteGoogleCalendarOAuthCommand(string Code, Guid MemberId, Guid FamilyId) : IRequest<GoogleCalendarStatusDto>;
public record DisconnectGoogleCalendarCommand() : IRequest;
public record TriggerGoogleCalendarSyncCommand() : IRequest<int>;

public class GoogleCalendarHandlers :
    IRequestHandler<GetGoogleCalendarAuthUrlQuery, string>,
    IRequestHandler<GetGoogleCalendarStatusQuery, GoogleCalendarStatusDto>,
    IRequestHandler<CompleteGoogleCalendarOAuthCommand, GoogleCalendarStatusDto>,
    IRequestHandler<DisconnectGoogleCalendarCommand>,
    IRequestHandler<TriggerGoogleCalendarSyncCommand, int>
{
    private readonly IFamilyOsDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IGoogleCalendarClient _google;
    private readonly IConfiguration _config;

    public GoogleCalendarHandlers(
        IFamilyOsDbContext db,
        ICurrentUserService current,
        IGoogleCalendarClient google,
        IConfiguration config)
    {
        _db = db;
        _current = current;
        _google = google;
        _config = config;
    }

    public Task<string> Handle(GetGoogleCalendarAuthUrlQuery query, CancellationToken ct)
    {
        EnsureConfigured();
        return Task.FromResult(_current.IsAuthenticated && _current.MemberId.HasValue && _current.FamilyId.HasValue
            ? BuildAuthUrlFor(_current.MemberId.Value, _current.FamilyId.Value)
            : throw new ForbiddenException("Not authenticated."));
    }

    public async Task<GoogleCalendarStatusDto> Handle(GetGoogleCalendarStatusQuery query, CancellationToken ct)
    {
        await _current.EnsureLoadedAsync(ct);
        var configured = IsConfigured();
        if (!_current.IsAuthenticated || !_current.MemberId.HasValue)
            return new GoogleCalendarStatusDto(false, null, null, null, configured);

        var conn = await _db.GoogleCalendarConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.MemberId == _current.MemberId && c.IsActive, ct);

        return conn is null
            ? new GoogleCalendarStatusDto(false, null, null, null, configured)
            : new GoogleCalendarStatusDto(true, conn.GoogleAccountEmail, conn.CalendarId, conn.LastSyncAtUtc, configured);
    }

    public async Task<GoogleCalendarStatusDto> Handle(CompleteGoogleCalendarOAuthCommand cmd, CancellationToken ct)
    {
        EnsureConfigured();
        var tokens = await _google.ExchangeCodeAsync(cmd.Code, ct);
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new DomainException("Google did not return a refresh token. Revoke prior access and try again with prompt=consent.");

        var email = await _google.GetAccountEmailAsync(tokens.AccessToken, ct) ?? string.Empty;
        var expires = DateTime.UtcNow.AddSeconds(Math.Max(60, tokens.ExpiresInSeconds));

        var existing = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.MemberId == cmd.MemberId, ct);

        if (existing is null)
        {
            existing = GoogleCalendarConnection.Create(
                cmd.FamilyId,
                cmd.MemberId,
                email,
                tokens.AccessToken,
                tokens.RefreshToken,
                expires);
            _db.GoogleCalendarConnections.Add(existing);
        }
        else
        {
            existing.UpdateTokens(tokens.AccessToken, expires, tokens.RefreshToken);
            if (!string.IsNullOrWhiteSpace(email))
            {
                // email is private set — reconnect path via Disconnect + Create is cleaner,
                // but UpdateTokens already re-activates. Store email via recreate if empty.
            }
        }

        await _db.SaveChangesAsync(ct);
        return new GoogleCalendarStatusDto(true, existing.GoogleAccountEmail, existing.CalendarId, existing.LastSyncAtUtc, true);
    }

    public async Task Handle(DisconnectGoogleCalendarCommand cmd, CancellationToken ct)
    {
        var (_, memberId, _) = await _current.RequireFamilyAsync(ct);
        var conn = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.IsActive, ct);
        if (conn is null) return;
        conn.Disconnect();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(TriggerGoogleCalendarSyncCommand cmd, CancellationToken ct)
    {
        var (_, memberId, _) = await _current.RequireFamilyAsync(ct);
        var conn = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.IsActive, ct)
            ?? throw new DomainException("Google Calendar is not connected.");

        return await GoogleCalendarSync.SyncConnectionAsync(_db, _google, conn, ct);
    }

    private string BuildAuthUrlFor(Guid memberId, Guid familyId)
    {
        var state = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{memberId:N}|{familyId:N}|{Guid.NewGuid():N}"));
        return _google.BuildAuthorizationUrl(state);
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_config["Google:ClientId"])
        && !string.IsNullOrWhiteSpace(_config["Google:ClientSecret"])
        && !string.IsNullOrWhiteSpace(_config["Google:RedirectUri"]);

    private void EnsureConfigured()
    {
        if (!IsConfigured())
            throw new DomainException("Google Calendar is not configured. Set Google:ClientId, ClientSecret, and RedirectUri.");
    }
}

/// <summary>Shared pull-sync logic used by the manual trigger and background worker.</summary>
public static class GoogleCalendarSync
{
    public const string Provider = "google";

    public static async Task EnsureValidAccessTokenAsync(
        IFamilyOsDbContext db,
        IGoogleCalendarClient google,
        GoogleCalendarConnection conn,
        CancellationToken ct)
    {
        if (!conn.AccessTokenExpired()) return;

        var refreshed = await google.RefreshAccessTokenAsync(conn.RefreshToken, ct);
        var expires = DateTime.UtcNow.AddSeconds(Math.Max(60, refreshed.ExpiresInSeconds));
        conn.UpdateTokens(refreshed.AccessToken, expires, refreshed.RefreshToken);
        await db.SaveChangesAsync(ct);
    }

    public static async Task<int> SyncConnectionAsync(
        IFamilyOsDbContext db,
        IGoogleCalendarClient google,
        GoogleCalendarConnection conn,
        CancellationToken ct)
    {
        await EnsureValidAccessTokenAsync(db, google, conn, ct);

        GoogleEventListResult list;
        try
        {
            list = await google.ListEventsAsync(
                conn.AccessToken,
                conn.CalendarId,
                timeMinUtc: conn.SyncToken is null ? DateTime.UtcNow.AddDays(-7) : null,
                timeMaxUtc: conn.SyncToken is null ? DateTime.UtcNow.AddDays(60) : null,
                syncToken: conn.SyncToken,
                ct: ct);
        }
        catch (InvalidOperationException) when (conn.SyncToken is not null)
        {
            // Sync token expired — full window resync
            conn.MarkSynced(null);
            await db.SaveChangesAsync(ct);
            list = await google.ListEventsAsync(
                conn.AccessToken,
                conn.CalendarId,
                timeMinUtc: DateTime.UtcNow.AddDays(-7),
                timeMaxUtc: DateTime.UtcNow.AddDays(60),
                ct: ct);
        }

        var changed = 0;
        foreach (var g in list.Events)
        {
            var existing = await db.CalendarEvents
                .FirstOrDefaultAsync(
                    e => e.FamilyId == conn.FamilyId
                         && e.ExternalProvider == Provider
                         && e.ExternalEventId == g.Id,
                    ct);

            if (g.Cancelled)
            {
                if (existing is not null && !existing.IsCancelled)
                {
                    existing.Cancel();
                    changed++;
                }
                continue;
            }

            if (g.StartUtc is null) continue;
            var title = string.IsNullOrWhiteSpace(g.Title) ? "(Google event)" : g.Title;

            if (existing is null)
            {
                var ev = CalendarEvent.CreateFromExternal(
                    conn.FamilyId,
                    conn.MemberId,
                    Provider,
                    g.Id,
                    conn.CalendarId,
                    title,
                    g.StartUtc.Value,
                    g.EndUtc,
                    g.AllDay,
                    g.Location,
                    g.Description);
                db.CalendarEvents.Add(ev);
                changed++;
            }
            else
            {
                existing.ApplyExternalUpdate(title, g.StartUtc.Value, g.EndUtc, g.AllDay, g.Location, g.Description);
                changed++;
            }
        }

        conn.MarkSynced(list.NextSyncToken);
        await db.SaveChangesAsync(ct);
        return changed;
    }

    public static async Task TryPushEventAsync(
        IFamilyOsDbContext db,
        IGoogleCalendarClient google,
        CalendarEvent ev,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(ev.ExternalEventId)) return;

        var conn = await db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.MemberId == ev.CreatedByMemberId && c.IsActive, ct);
        if (conn is null) return;

        try
        {
            await EnsureValidAccessTokenAsync(db, google, conn, ct);
            var result = await google.InsertEventAsync(
                conn.AccessToken,
                conn.CalendarId,
                new GoogleEventInput(ev.Title, ev.StartUtc, ev.EndUtc, ev.AllDay, ev.Location, ev.Description),
                ct);
            ev.LinkExternal(Provider, result.Id, conn.CalendarId);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Push is best-effort; local event remains the source of truth.
        }
    }
}
