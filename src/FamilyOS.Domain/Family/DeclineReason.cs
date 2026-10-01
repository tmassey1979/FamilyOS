using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

public class DeclineReason : Entity
{
    public Guid FamilyId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    private DeclineReason() { }

    public static DeclineReason Create(Guid familyId, string text, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text required.");
        return new DeclineReason
        {
            FamilyId = familyId,
            Text = text.Trim(),
            SortOrder = sortOrder
        };
    }

    public void Update(string text, int sortOrder)
    {
        Text = text.Trim();
        SortOrder = sortOrder;
        MarkUpdated();
    }

    public void Disable() { IsEnabled = false; MarkUpdated(); }
    public void Enable() { IsEnabled = true; MarkUpdated(); }
}
