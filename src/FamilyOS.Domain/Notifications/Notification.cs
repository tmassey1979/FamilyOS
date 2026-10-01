using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Notifications;

public class Notification : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid RecipientMemberId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public Guid? RelatedEntityId { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public bool IsActioned { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid familyId,
        Guid recipientMemberId,
        NotificationType type,
        string title,
        string body,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        return new Notification
        {
            FamilyId = familyId,
            RecipientMemberId = recipientMemberId,
            Type = type,
            Title = title.Trim(),
            Body = body.Trim(),
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType
        };
    }

    public void MarkRead()
    {
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarkActioned()
    {
        IsActioned = true;
        if (!IsRead) MarkRead();
        MarkUpdated();
    }
}
