using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Messaging;

/// <summary>
/// After successful SaveChanges, publish any domain events collected on tracked entities.
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DomainEventDispatchInterceptor> _log;

    public DomainEventDispatchInterceptor(IServiceProvider sp, ILogger<DomainEventDispatchInterceptor> log)
    {
        _sp = sp;
        _log = log;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await DispatchAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
            DispatchAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken ct)
    {
        var entities = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entities.Count == 0) return;

        // Resolve bus from scope — interceptor is singleton; bus is scoped
        using var scope = _sp.CreateScope();
        var bus = scope.ServiceProvider.GetService<IDomainEventBus>();
        if (bus is null) return;

        foreach (var entity in entities)
        {
            var events = entity.DomainEvents.ToList();
            entity.ClearDomainEvents();
            foreach (var domainEvent in events)
            {
                try
                {
                    // Dynamic dispatch through generic PublishAsync
                    var method = typeof(IDomainEventBus).GetMethod(nameof(IDomainEventBus.PublishAsync))!;
                    var generic = method.MakeGenericMethod(domainEvent.GetType());
                    var task = (Task)generic.Invoke(bus, new object[] { domainEvent, ct })!;
                    await task;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to publish domain event {EventType}", domainEvent.GetType().Name);
                }
            }
        }
    }
}
