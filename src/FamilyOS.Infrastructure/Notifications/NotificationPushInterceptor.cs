using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Notifications;

/// <summary>
/// After SaveChanges, best-effort Expo push for every newly inserted Notification.
/// Covers Request/Task handlers that Add(Notification) directly without CreateNotificationCommand.
/// </summary>
public sealed class NotificationPushInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationPushInterceptor> _logger;

    public NotificationPushInterceptor(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationPushInterceptor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null || result <= 0)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        // Entries already saved; use ChangeTracker for entities that were Added this round.
        // After save they are Unchanged — capture from Entries that still have Entity type Notification.
        // Safer: collect before save via SavingChanges.
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private List<Notification>? _pending;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? ctx)
    {
        if (ctx is null) return;
        _pending = ctx.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // fire-and-forget sync path not used in API
        _ = PushPendingAsync(CancellationToken.None);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PushPendingAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PushPendingAsync(CancellationToken ct)
    {
        var list = _pending;
        _pending = null;
        if (list is null || list.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();
            foreach (var n in list)
            {
                var data = new Dictionary<string, string>
                {
                    ["notificationId"] = n.Id.ToString()
                };
                if (n.RelatedEntityId.HasValue)
                    data["relatedEntityId"] = n.RelatedEntityId.Value.ToString();
                if (!string.IsNullOrWhiteSpace(n.RelatedEntityType))
                    data["relatedEntityType"] = n.RelatedEntityType!;

                try
                {
                    await push.SendToMemberAsync(n.RecipientMemberId, n.Title, n.Body, data, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Push failed for notification {NotificationId} member {MemberId}",
                        n.Id, n.RecipientMemberId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push interceptor failed");
        }
    }
}
