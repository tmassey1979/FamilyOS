namespace FamilyOS.Application.Interfaces;

/// <summary>Realtime fan-out for family chat (SignalR implementation in Infrastructure/Api).</summary>
public interface IFamilyChatNotifier
{
    Task MessageSentAsync(Guid familyId, ChatMessageRealtimeDto message, CancellationToken ct = default);
}

public record ChatMessageRealtimeDto(
    Guid Id,
    Guid ChannelId,
    Guid SenderMemberId,
    string SenderName,
    string Body,
    DateTime SentAtUtc);
