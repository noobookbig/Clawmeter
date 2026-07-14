using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clawmeter.Service.Services;
using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Per-provider polling strategy. C# port of daemon/payloads.py:
/// build_minimax_usage_payload() — produces a UsagePayload byte-for-byte
/// identical to the Python daemon, so the ESP32 firmware's usage_parse_json
/// sees the same JSON shape it has always seen.
///
/// Each strategy class encapsulates one provider. New providers land in
/// additional classes implementing the same IProviderPollStrategy interface.
/// </summary>
public sealed class ProviderPollerService
{
    private readonly ILogger<ProviderPollerService> _log;
    private readonly ConfigService _config;
    private readonly HttpClient _http;
    private readonly Dictionary<ProviderId, IProviderPollStrategy> _strategies;

    public ProviderPollerService(
        ILogger<ProviderPollerService> log,
        ConfigService config,
        IHttpClientFactory? httpFactory = null)
    {
        _log = log;
        _config = config;
        _http = httpFactory?.CreateClient(nameof(ProviderPollerService))
                  ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Clawmeter", "1.0"));

        _strategies = new Dictionary<ProviderId, IProviderPollStrategy>
        {
            [ProviderId.Minimax] = new MiniMaxPollStrategy(_http, _log),
            // Claude / Codex / OpenRouter / etc land in follow-up PRs.
        };
    }

    public Task<UsagePayload?> PollAsync(ProviderId provider, CancellationToken ct)
    {
        if (!_strategies.TryGetValue(provider, out var s))
        {
            _log.LogWarning("No poll strategy for provider {Provider}", provider);
            return Task.FromResult<UsagePayload?>(null);
        }
        return s.PollAsync(_config, ct);
    }
}

/// <summary>Common interface for a provider-specific polling strategy.</summary>
public interface IProviderPollStrategy
{
    Task<UsagePayload?> PollAsync(ConfigService cfg, CancellationToken ct);
}

/// <summary>
/// C# port of build_minimax_usage_payload. Pulls /v1/token_plan/remains,
/// picks the best text/general lane via the same scoring heuristic, and
/// emits a UsagePayload that matches the Python daemon's wire output
/// exactly (firmware parses unchanged).
/// </summary>
public sealed class MiniMaxPollStrategy : IProviderPollStrategy
{
    private const string BaseUrl = "https://api.minimax.io/v1/token_plan/remains";
    private static readonly string[] FallbackUrls =
    {
        "https://www.minimax.io/v1/token_plan/remains",
        "https://api.minimaxi.com/v1/token_plan/remains",
        "https://www.minimaxi.com/v1/token_plan/remains",
    };

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ILogger _log;

    public MiniMaxPollStrategy(HttpClient http, ILogger log)
    {
        _http = http;
        _log  = log;
    }

    public async Task<UsagePayload?> PollAsync(ConfigService cfg, CancellationToken ct)
    {
        var apiKey = cfg.GetMiniMaxApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            _log.LogWarning("MiniMax API key not configured — skipping poll");
            return null;
        }

        JsonElement? root = null;
        foreach (var url in new[] { BaseUrl }.Concat(FallbackUrls))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                req.Headers.Add("MM-API-Source", "Clawdmeter");
                using var resp = await _http.SendAsync(req, ct);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _log.LogWarning("MiniMax auth failed (401) — check API key");
                    return null;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("MiniMax {Url} returned {Code}", url, (int)resp.StatusCode);
                    continue;
                }
                var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, ReadOpts, ct);
                root = doc.RootElement;
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "MiniMax {Url} fetch failed", url);
            }
        }
        if (root is null) return null;

        // Build the payload the way build_minimax_usage_payload() does in
        // daemon/payloads.py: pick the best text/general item, derive rolling
        // + weekly remaining percentages, compute reset minutes, classify
        // status thresholds, return the wire-format UsagePayload.
        var modelItem = PickBestModelItem(root.Value);
        if (modelItem is null) return null;

        var now      = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rolling  = RemainingPct(modelItem.Value, "current_interval");
        var weekly   = RemainingPct(modelItem.Value, "current_weekly") ?? rolling;
        var topReset    = ResetMins(modelItem.Value, "current_interval", now);
        var bottomReset = ResetMins(modelItem.Value, "current_weekly",  now);

        string status = "allowed";
        if (rolling is int r && weekly is int w)
        {
            if (r <= 10 || w <= 10) status = "limited";
            else if (r <= 25 || w <= 25) status = "warning";
        }

        return new UsagePayload
        {
            Provider = "minimax",
            Mode     = "window",
            Status   = status,
            Ok       = true,
            Top      = new PanelPayload
            {
                Label     = "Current",
                Kind      = "window_short",
                Pct       = rolling ?? 0,
                ResetMins = topReset,
                HasReset  = true,
            },
            Bottom   = new PanelPayload
            {
                Label     = "Weekly",
                Kind      = "window_long",
                Pct       = weekly ?? 0,
                ResetMins = bottomReset,
                HasReset  = true,
            },
        };
    }

    /// <summary>Best text/general lane via the same chat_score() heuristic.</summary>
    private static JsonElement? PickBestModelItem(JsonElement root)
    {
        JsonElement? data = root;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
            data = d;
        if (data is null || data.Value.ValueKind != JsonValueKind.Object) return null;
        if (!data.Value.TryGetProperty("model_remains", out var arr) ||
            arr.ValueKind != JsonValueKind.Array) return null;

        var items = arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .ToList();
        if (items.Count == 0) return null;

        // Drop image / video / audio lanes
        var textOnly = items.Where(e => !IsNonTextLane(e)).ToList();
        if (textOnly.Count > 0) items = textOnly;

        return items.MaxBy(e => ChatScore(e));
    }

    private static bool IsNonTextLane(JsonElement item)
    {
        var name = (GetString(item, "model_name", "modelName", "service_type", "serviceType") ?? "")
            .ToLowerInvariant();
        return name.Contains("image") || name.Contains("video") ||
               name.Contains("speech") || name.Contains("music") || name.Contains("audio");
    }

    private static int ChatScore(JsonElement item)
    {
        var name = (GetString(item, "model_name", "modelName", "service_type", "serviceType") ?? "")
            .ToLowerInvariant();
        int score = 0;
        if (name.StartsWith("minimax-m"))    score += 1_000;
        else if (name is "general" or "text" or "text-generation") score += 500;
        else if (name.Contains("minimax"))    score += 10;
        if (new[] { "m3", "m2", "text", "chat", "coding" }.Any(name.Contains)) score += 100;
        if (GetNumber(item, "current_interval_total_count", "currentIntervalTotalCount") is > 0) score += 10_000;
        if (GetNumber(item, "current_weekly_total_count",   "currentWeeklyTotalCount")  is > 0) score += 1_000;
        return score;
    }

    private static int? RemainingPct(JsonElement item, string window)
    {
        var total  = GetNumber(item, $"{window}_total_count",  $"{window}TotalCount");
        var remain = GetNumber(item, $"{window}_usage_count",  $"{window}UsageCount");
        if (total is > 0 && remain is not null)
            return (int)Math.Round(remain.Value / total.Value * 100.0);
        var explicit = GetNumber(item,
            $"{window}_remaining_percent", $"{window}RemainingPercent",
            "usage_percent", "usagePercent");
        return explicit is null ? null : (int)Math.Round(explicit.Value);
    }

    private static int ResetMins(JsonElement item, string window, long now)
    {
        var end = GetNumber(item,
            $"{window}_end_time", $"{window}EndTime",
            "end_time", "endTime");
        if (end is > 10_000_000_000) end /= 1000;   // ms → s
        if (end is > now) return (int)Math.Round((end.Value - now) / 60.0);

        var secs = GetNumber(item,
            $"{window}_remains_time", $"{window}RemainsTime",
            "remains_time", "remainsTime");
        if (secs is > 864_000) secs /= 1000;          // ms → s
        return secs is > 0 ? (int)Math.Round(secs.Value / 60.0) : 0;
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
        return null;
    }

    private static double? GetNumber(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
        return null;
    }
}
