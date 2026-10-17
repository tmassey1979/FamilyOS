using System.Net.Http.Json;
using System.Text.Json;

namespace FamilyOS.Api.Observability;

/// <summary>
/// Minimal PostHog capture client for server-side exception events.
/// No-ops when PostHog:ApiKey is unset.
/// </summary>
public interface IPostHogErrorReporter
{
    Task CaptureExceptionAsync(Exception ex, HttpContext? http, CancellationToken ct = default);
}

public sealed class PostHogErrorReporter : IPostHogErrorReporter
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PostHogErrorReporter> _log;

    public PostHogErrorReporter(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<PostHogErrorReporter> log)
    {
        _httpFactory = httpFactory;
        _config = config;
        _log = log;
    }

    public async Task CaptureExceptionAsync(Exception ex, HttpContext? http, CancellationToken ct = default)
    {
        var apiKey = _config["PostHog:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var host = (_config["PostHog:Host"] ?? "https://us.i.posthog.com").TrimEnd('/');
        try
        {
            var client = _httpFactory.CreateClient("posthog");
            var distinctId = http?.User?.FindFirst("sub")?.Value
                ?? http?.TraceIdentifier
                ?? "api";

            var payload = new
            {
                api_key = apiKey,
                event_ = "$exception",
                distinct_id = distinctId,
                properties = new Dictionary<string, object?>
                {
                    ["$exception_message"] = ex.Message,
                    ["$exception_type"] = ex.GetType().FullName,
                    ["$exception_stack_trace_raw"] = ex.ToString(),
                    ["path"] = http?.Request.Path.Value,
                    ["method"] = http?.Request.Method,
                    ["traceId"] = http?.TraceIdentifier,
                    ["source"] = "FamilyOS.Api"
                }
            };

            // PostHog expects "event" not "event_" — serialize manually
            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["api_key"] = apiKey,
                ["event"] = "$exception",
                ["distinct_id"] = distinctId,
                ["properties"] = payload.properties
            });

            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var res = await client.PostAsync($"{host}/i/v0/e/", content, ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogDebug("PostHog capture returned {Status}", (int)res.StatusCode);
            }
        }
        catch (Exception captureEx)
        {
            _log.LogDebug(captureEx, "PostHog error capture failed");
        }
    }
}
