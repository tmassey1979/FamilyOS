using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Family;

public class FamilyMember : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public FamilyRole Role { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    /// <summary>When true, suppress non-urgent notifications between Start/End (local minutes from midnight).</summary>
    public bool QuietHoursEnabled { get; private set; }
    /// <summary>Start of quiet window as minutes from local midnight (0–1439).</summary>
    public int? QuietHoursStartMinutes { get; private set; }
    /// <summary>End of quiet window as minutes from local midnight (0–1439). Supports overnight windows.</summary>
    public int? QuietHoursEndMinutes { get; private set; }

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

    public void SetQuietHours(bool enabled, int? startMinutes, int? endMinutes)
    {
        if (enabled)
        {
            if (startMinutes is null || endMinutes is null)
                throw new ArgumentException("Start and end minutes required when quiet hours are enabled.");
            if (startMinutes < 0 || startMinutes > 1439 || endMinutes < 0 || endMinutes > 1439)
                throw new ArgumentException("Quiet hours must be between 0 and 1439 minutes.");
        }
        QuietHoursEnabled = enabled;
        QuietHoursStartMinutes = enabled ? startMinutes : null;
        QuietHoursEndMinutes = enabled ? endMinutes : null;
        MarkUpdated();
    }

    /// <summary>True if local time falls inside the quiet window (supports overnight).</summary>
    public bool IsInQuietHours(TimeSpan localTimeOfDay)
    {
        if (!QuietHoursEnabled || QuietHoursStartMinutes is null || QuietHoursEndMinutes is null)
            return false;
        var mins = (int)localTimeOfDay.TotalMinutes;
        var start = QuietHoursStartMinutes.Value;
        var end = QuietHoursEndMinutes.Value;
        if (start == end) return true; // full-day quiet
        if (start < end) return mins >= start && mins < end;
        // Overnight: e.g. 22:00–07:00
        return mins >= start || mins < end;
    }
}
