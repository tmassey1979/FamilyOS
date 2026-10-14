using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Chat;

public enum ChatChannelKind
{
    Family = 0,
    Direct = 1
}

/// <summary>A chat room scoped to a family (default: one shared Family channel).</summary>
public class ChatChannel : Entity
{
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ChatChannelKind Kind { get; private set; } = ChatChannelKind.Family;
    public bool IsActive { get; private set; } = true;

    private ChatChannel() { }

    public static ChatChannel CreateFamilyChannel(Guid familyId, string name = "Family")
    {
        return new ChatChannel
        {
            FamilyId = familyId,
            Name = string.IsNullOrWhiteSpace(name) ? "Family" : name.Trim(),
            Kind = ChatChannelKind.Family,
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name required.", nameof(name));
        Name = name.Trim();
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
