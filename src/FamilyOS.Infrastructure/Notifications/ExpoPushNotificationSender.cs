using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FamilyOS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Notifications;

/// <summary>
/// Sends via Expo Push API (https://exp.host/--/api/v2/push/send).
/// Tokens that are not Expo push tokens are skipped (local-* test tokens stay silent).
/// </summary>
public sealed class ExpoPushNotificationSender : IPushNotificationSender
{
    private static readonly Uri ExpoPushUri = new("https://exp.host/--/api/v2/push/send");
    private readonly IFamilyOsDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExpoPushNotificationSender> _logger;

    public ExpoPushNotificationSender(
        IFamilyOsDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<ExpoPushNotificationSender> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task SendToMemberAsync(
        Guid memberId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default) =>
        SendToMembersAsync(new[] { memberId }, title, body, data, ct);

    public async Task SendToMembersAsync(
        IEnumerable<Guid> memberIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return;

        List<string> tokens;
        try
        {
            tokens = await _db.DeviceRegistrations.AsNoTracking()
                .Where(d => ids.Contains(d.MemberId) && d.IsActive)
                .Select(d => d.Token)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push: failed to load device tokens");
            return;
        }

        tokens = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t)
                        && (t.StartsWith("ExponentPushToken[", StringComparison.Ordinal)
                            || t.StartsWith("ExpoPushToken[", StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        if (tokens.Count == 0) return;

        var messages = tokens.Select(t => new ExpoPushMessage
        {
            To = t,
            Title = title,
            Body = body,
            Sound = "default",
            Data = data
        }).ToList();

        try
        {
            var client = _httpClientFactory.CreateClient("ExpoPush");
            using var response = await client.PostAsJsonAsync(ExpoPushUri, messages, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Expo push failed {Status}: {Body}", (int)response.StatusCode, err);
                return;
            }

            _logger.LogInformation("Expo push sent to {Count} device(s)", tokens.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Expo push request failed");
        }
    }

    private sealed class ExpoPushMessage
    {
        [JsonPropertyName("to")]
        public string To { get; set; } = "";

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("sound")]
        public string? Sound { get; set; }

        [JsonPropertyName("data")]
        public IReadOnlyDictionary<string, string>? Data { get; set; }
    }
}

/// <summary>No-op for unit tests / hosts without push.</summary>
public sealed class NullPushNotificationSender : IPushNotificationSender
{
    public Task SendToMemberAsync(
        Guid memberId, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendToMembersAsync(
        IEnumerable<Guid> memberIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default) =>
        Task.CompletedTask;
}
