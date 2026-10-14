namespace FamilyOS.Application.Interfaces;

/// <summary>Best-effort push to a member's registered devices (Expo Push API).</summary>
public interface IPushNotificationSender
{
    Task SendToMemberAsync(
        Guid memberId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task SendToMembersAsync(
        IEnumerable<Guid> memberIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default);
}
