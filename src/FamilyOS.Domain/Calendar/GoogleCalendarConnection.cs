using FamilyOS.Domain.Common;

namespace FamilyOS.Domain.Calendar;

/// <summary>Per-member Google Calendar OAuth link for bidirectional sync.</summary>
public class GoogleCalendarConnection : Entity
{
    public Guid FamilyId { get; private set; }
    public Guid MemberId { get; private set; }
    public string GoogleAccountEmail { get; private set; } = string.Empty;
    public string AccessToken { get; private set; } = string.Empty;
    public string RefreshToken { get; private set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public string CalendarId { get; private set; } = "primary";
    public string? SyncToken { get; private set; }
    public DateTime? LastSyncAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private GoogleCalendarConnection() { }

    public static GoogleCalendarConnection Create(
        Guid familyId,
        Guid memberId,
        string email,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAtUtc,
        string calendarId = "primary")
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token required.", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token required.", nameof(refreshToken));

        return new GoogleCalendarConnection
        {
            FamilyId = familyId,
            MemberId = memberId,
            GoogleAccountEmail = (email ?? string.Empty).Trim().ToLowerInvariant(),
            AccessToken = accessToken.Trim(),
            RefreshToken = refreshToken.Trim(),
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            CalendarId = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId.Trim(),
            IsActive = true
        };
    }

    public void UpdateTokens(string accessToken, DateTime expiresAtUtc, string? refreshToken = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token required.", nameof(accessToken));
        AccessToken = accessToken.Trim();
        AccessTokenExpiresAtUtc = expiresAtUtc;
        if (!string.IsNullOrWhiteSpace(refreshToken))
            RefreshToken = refreshToken.Trim();
        IsActive = true;
        MarkUpdated();
    }

    public void MarkSynced(string? syncToken)
    {
        LastSyncAtUtc = DateTime.UtcNow;
        if (syncToken is not null)
            SyncToken = string.IsNullOrWhiteSpace(syncToken) ? null : syncToken;
        MarkUpdated();
    }

    public void SetCalendarId(string calendarId)
    {
        CalendarId = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId.Trim();
        SyncToken = null; // force full resync on calendar change
        MarkUpdated();
    }

    public void Disconnect()
    {
        IsActive = false;
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        SyncToken = null;
        MarkUpdated();
    }

    public bool AccessTokenExpired(TimeSpan? skew = null)
    {
        var margin = skew ?? TimeSpan.FromMinutes(2);
        return DateTime.UtcNow >= AccessTokenExpiresAtUtc - margin;
    }
}
