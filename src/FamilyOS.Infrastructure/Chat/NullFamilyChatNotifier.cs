using FamilyOS.Application.Interfaces;

namespace FamilyOS.Infrastructure.Chat;

/// <summary>Fallback when SignalR hub is not registered (e.g. pure unit host).</summary>
public sealed class NullFamilyChatNotifier : IFamilyChatNotifier
{
    public Task MessageSentAsync(Guid familyId, ChatMessageRealtimeDto message, CancellationToken ct = default) =>
        Task.CompletedTask;
}
