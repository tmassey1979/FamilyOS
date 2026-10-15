namespace FamilyOS.Application.Interfaces;

/// <summary>
/// Publishes integration / domain events (MassTransit under the hood).
/// Never throws to callers — failures are logged by the implementation.
/// </summary>
public interface IDomainEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
