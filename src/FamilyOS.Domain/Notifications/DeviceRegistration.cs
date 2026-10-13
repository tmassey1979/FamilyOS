using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Notifications;

/// <summary>Expo/FCM device token for a family member — foundation for push (#27).</summary>
public class DeviceRegistration : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid MemberId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Platform { get; private set; } = "unknown";
    public string? DeviceName { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; } = DateTime.UtcNow;
    public bool IsActive { get; private set; } = true;

    private DeviceRegistration() { }

    public static DeviceRegistration Create(
        Guid familyId,
        Guid memberId,
        string token,
        string platform,
        string? deviceName = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        return new DeviceRegistration
        {
            FamilyId = familyId,
            MemberId = memberId,
            Token = token.Trim(),
            Platform = string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim().ToLowerInvariant(),
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            LastSeenAtUtc = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Touch(string? platform = null, string? deviceName = null)
    {
        LastSeenAtUtc = DateTime.UtcNow;
        IsActive = true;
        if (!string.IsNullOrWhiteSpace(platform))
            Platform = platform.Trim().ToLowerInvariant();
        if (deviceName is not null)
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim();
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
