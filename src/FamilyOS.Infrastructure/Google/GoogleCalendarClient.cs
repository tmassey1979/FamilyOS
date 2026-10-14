using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyOS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilyOS.Infrastructure.Google;

public sealed class GoogleCalendarClient : IGoogleCalendarClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleCalendarClient> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GoogleCalendarClient(HttpClient http, IConfiguration config, ILogger<GoogleCalendarClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    private string ClientId => _config["Google:ClientId"] ?? string.Empty;
    private string ClientSecret => _config["Google:ClientSecret"] ?? string.Empty;
    private string RedirectUri => _config["Google:RedirectUri"] ?? string.Empty;

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = Uri.EscapeDataString("https://www.googleapis.com/auth/calendar https://www.googleapis.com/auth/userinfo.email");
        return "https://accounts.google.com/o/oauth2/v2/auth"
               + "?client_id=" + Uri.EscapeDataString(ClientId)
               + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
               + "&response_type=code"
               + "&scope=" + scopes
               + "&access_type=offline"
               + "&prompt=consent"
               + "&state=" + Uri.EscapeDataString(state);
    }

    public async Task<GoogleTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code"
        });
        using var res = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token exchange failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("Google token exchange failed.");
        }
        var parsed = JsonSerializer.Deserialize<TokenJson>(body, JsonOpts)
                     ?? throw new InvalidOperationException("Invalid token response.");
        return new GoogleTokenResponse(parsed.AccessToken!, parsed.RefreshToken, parsed.ExpiresIn, parsed.Scope);
    }

    public async Task<GoogleTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "refresh_token"
        });
        using var res = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token refresh failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("Google token refresh failed.");
        }
        var parsed = JsonSerializer.Deserialize<TokenJson>(body, JsonOpts)
                     ?? throw new InvalidOperationException("Invalid token response.");
        return new GoogleTokenResponse(parsed.AccessToken!, parsed.RefreshToken ?? refreshToken, parsed.ExpiresIn, parsed.Scope);
    }

    public async Task<string?> GetAccountEmailAsync(string accessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
    }

    public async Task<GoogleEventResult> InsertEventAsync(
        string accessToken, string calendarId, GoogleEventInput input, CancellationToken ct = default)
    {
        var payload = ToGoogleEventBody(input);
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google insert event failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("Google insert event failed.");
        }
        return ParseEvent(body);
    }

    public async Task<GoogleEventResult?> UpdateEventAsync(
        string accessToken, string calendarId, string eventId, GoogleEventInput input, CancellationToken ct = default)
    {
        var payload = ToGoogleEventBody(input);
        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google update event failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("Google update event failed.");
        }
        return ParseEvent(body);
    }

    public async Task DeleteEventAsync(string accessToken, string calendarId, string eventId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound && res.StatusCode != System.Net.HttpStatusCode.Gone)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Google delete event failed: {Status} {Body}", (int)res.StatusCode, body);
        }
    }

    public async Task<GoogleEventListResult> ListEventsAsync(
        string accessToken,
        string calendarId,
        DateTime? timeMinUtc = null,
        DateTime? timeMaxUtc = null,
        string? syncToken = null,
        CancellationToken ct = default)
    {
        var url = new StringBuilder(
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?singleEvents=true&showDeleted=true&maxResults=250");
        if (!string.IsNullOrWhiteSpace(syncToken))
        {
            url.Append("&syncToken=").Append(Uri.EscapeDataString(syncToken));
        }
        else
        {
            if (timeMinUtc.HasValue)
                url.Append("&timeMin=").Append(Uri.EscapeDataString(timeMinUtc.Value.ToUniversalTime().ToString("o")));
            if (timeMaxUtc.HasValue)
                url.Append("&timeMax=").Append(Uri.EscapeDataString(timeMaxUtc.Value.ToUniversalTime().ToString("o")));
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if ((int)res.StatusCode == 410)
            throw new InvalidOperationException("Google sync token expired.");

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google list events failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("Google list events failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var events = new List<GoogleEventResult>();
        if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
                events.Add(ParseEventElement(item));
        }

        string? nextSync = null;
        if (doc.RootElement.TryGetProperty("nextSyncToken", out var st))
            nextSync = st.GetString();

        return new GoogleEventListResult(events, nextSync);
    }

    private static object ToGoogleEventBody(GoogleEventInput input)
    {
        if (input.AllDay)
        {
            var startDate = input.StartUtc.ToUniversalTime().ToString("yyyy-MM-dd");
            var endDate = (input.EndUtc ?? input.StartUtc.AddDays(1)).ToUniversalTime().ToString("yyyy-MM-dd");
            return new
            {
                summary = input.Title,
                description = input.Description,
                location = input.Location,
                start = new { date = startDate },
                end = new { date = endDate }
            };
        }

        var end = input.EndUtc ?? input.StartUtc.AddHours(1);
        return new
        {
            summary = input.Title,
            description = input.Description,
            location = input.Location,
            start = new { dateTime = input.StartUtc.ToUniversalTime().ToString("o"), timeZone = "UTC" },
            end = new { dateTime = end.ToUniversalTime().ToString("o"), timeZone = "UTC" }
        };
    }

    private static GoogleEventResult ParseEvent(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return ParseEventElement(doc.RootElement);
    }

    private static GoogleEventResult ParseEventElement(JsonElement el)
    {
        var id = el.GetProperty("id").GetString() ?? string.Empty;
        var status = el.TryGetProperty("status", out var st) ? st.GetString() : null;
        var cancelled = string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);
        var title = el.TryGetProperty("summary", out var sum) ? sum.GetString() ?? "" : "";
        var location = el.TryGetProperty("location", out var loc) ? loc.GetString() : null;
        var description = el.TryGetProperty("description", out var desc) ? desc.GetString() : null;

        bool allDay = false;
        DateTime? start = null;
        DateTime? end = null;

        if (el.TryGetProperty("start", out var startEl))
        {
            if (startEl.TryGetProperty("date", out var sd))
            {
                allDay = true;
                if (DateTime.TryParse(sd.GetString(), out var d)) start = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            }
            else if (startEl.TryGetProperty("dateTime", out var sdt) && DateTime.TryParse(sdt.GetString(), out var dt))
            {
                start = dt.ToUniversalTime();
            }
        }

        if (el.TryGetProperty("end", out var endEl))
        {
            if (endEl.TryGetProperty("date", out var ed))
            {
                if (DateTime.TryParse(ed.GetString(), out var d)) end = DateTime.SpecifyKind(d, DateTimeKind.Utc);
            }
            else if (endEl.TryGetProperty("dateTime", out var edt) && DateTime.TryParse(edt.GetString(), out var dt))
            {
                end = dt.ToUniversalTime();
            }
        }

        return new GoogleEventResult(id, status, title, start, end, allDay, location, description, cancelled);
    }

    private sealed class TokenJson
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }
}
