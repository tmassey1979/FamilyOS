using FamilyOS.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Messaging;

public sealed class MassTransitDomainEventBus : IDomainEventBus
{
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<MassTransitDomainEventBus> _log;

    public MassTransitDomainEventBus(IPublishEndpoint publish, ILogger<MassTransitDomainEventBus> log)
    {
        _publish = publish;
        _log = log;
    }

    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        try
        {
            await _publish.Publish(message, ct);
        }
        catch (Exception ex)
        {
            // Never fail the request path because the bus is down
            _log.LogWarning(ex, "Domain event publish failed for {EventType}", typeof(T).Name);
        }
    }
}
