using FamilyOS.Application.Calendar;
using FamilyOS.Application.Interfaces;
using FamilyOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FamilyOS.Infrastructure.Google;

/// <summary>Timed service: polls each connected member's Google Calendar and pulls events into FamilyOS.</summary>
public sealed class GoogleCalendarSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoogleCalendarSyncWorker> _logger;
    private readonly TimeSpan _interval;

    public GoogleCalendarSyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<GoogleCalendarSyncWorker> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = 5;
        if (int.TryParse(config["Google:SyncIntervalMinutes"], out var m) && m > 0)
            minutes = Math.Clamp(m, 1, 120);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Google Calendar sync worker started (interval {Minutes} min)", _interval.TotalMinutes);
        // Initial delay so the API can finish boot / seed
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Google Calendar sync cycle failed");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
        var google = scope.ServiceProvider.GetRequiredService<IGoogleCalendarClient>();

        var connections = await db.GoogleCalendarConnections
            .Where(c => c.IsActive && !c.IsDeleted)
            .ToListAsync(ct);

        if (connections.Count == 0) return;

        _logger.LogInformation("Syncing {Count} Google Calendar connection(s)", connections.Count);

        foreach (var conn in connections)
        {
            try
            {
                var changed = await GoogleCalendarSync.SyncConnectionAsync(db, google, conn, ct);
                if (changed > 0)
                    _logger.LogInformation(
                        "Google sync member {MemberId}: {Changed} event change(s)",
                        conn.MemberId, changed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google sync failed for member {MemberId}", conn.MemberId);
            }
        }
    }
}
