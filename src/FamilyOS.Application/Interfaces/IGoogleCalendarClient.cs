namespace FamilyOS.Application.Interfaces;

/// <summary>Google Calendar REST client (OAuth + events). Implemented in Infrastructure.</summary>
public interface IGoogleCalendarClient
{
    string BuildAuthorizationUrl(string state);

    Task<GoogleTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default);

    Task<GoogleTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default);

    Task<string?> GetAccountEmailAsync(string accessToken, CancellationToken ct = default);

    Task<GoogleEventResult> InsertEventAsync(
        string accessToken,
        string calendarId,
        GoogleEventInput input,
        CancellationToken ct = default);

    Task<GoogleEventResult?> UpdateEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        GoogleEventInput input,
        CancellationToken ct = default);

    Task DeleteEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CancellationToken ct = default);

    Task<GoogleEventListResult> ListEventsAsync(
        string accessToken,
        string calendarId,
        DateTime? timeMinUtc = null,
        DateTime? timeMaxUtc = null,
        string? syncToken = null,
        CancellationToken ct = default);
}

public record GoogleTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresInSeconds,
    string? Scope);

public record GoogleEventInput(
    string Title,
    DateTime StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Location,
    string? Description);

public record GoogleEventResult(
    string Id,
    string? Status,
    string Title,
    DateTime? StartUtc,
    DateTime? EndUtc,
    bool AllDay,
    string? Location,
    string? Description,
    bool Cancelled);

public record GoogleEventListResult(
    IReadOnlyList<GoogleEventResult> Events,
    string? NextSyncToken);
