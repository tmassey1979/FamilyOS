using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

public class FamilyMember : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public FamilyRole Role { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private FamilyMember() { }

    public static FamilyMember Create(Guid familyId, Guid userId, FamilyRole role, string displayName)
    {
        if (familyId == Guid.Empty) throw new ArgumentException("FamilyId required.");
        if (userId == Guid.Empty) throw new ArgumentException("UserId required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name required.");

        return new FamilyMember
        {
            FamilyId = familyId,
            UserId = userId,
            Role = role,
            DisplayName = displayName.Trim()
        };
    }

    public void ChangeRole(FamilyRole newRole)
    {
        Role = newRole;
        MarkUpdated();
    }

    public void UpdateDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Display name required.");
        DisplayName = name.Trim();
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    public bool CanApproveRequests => Role is FamilyRole.Owner or FamilyRole.Adult;
    public bool CanManageFamily => Role == FamilyRole.Owner;
    public bool CanAssignTasks => Role is FamilyRole.Owner or FamilyRole.Adult;
    public bool CanOverridePolicies => Role == FamilyRole.Owner;
}
