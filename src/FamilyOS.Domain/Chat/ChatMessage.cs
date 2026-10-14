using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Chat;

public class ChatMessage : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid ChannelId { get; private set; }
    public Guid SenderMemberId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAtUtc { get; private set; } = DateTime.UtcNow;

    private ChatMessage() { }

    public static ChatMessage Create(Guid familyId, Guid channelId, Guid senderMemberId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));
        var trimmed = body.Trim();
        if (trimmed.Length > 4000)
            throw new ArgumentException("Message is too long (max 4000 characters).", nameof(body));

        return new ChatMessage
        {
            FamilyId = familyId,
            ChannelId = channelId,
            SenderMemberId = senderMemberId,
            Body = trimmed,
            SentAtUtc = DateTime.UtcNow
        };
    }
}
