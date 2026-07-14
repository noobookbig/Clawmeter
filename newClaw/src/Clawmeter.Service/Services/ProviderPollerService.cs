using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Per-provider polling strategy. One strategy class per ProviderId
/// (MiniMax today; Claude / Codex / OpenRouter land in M9).
/// Each builds a UsagePayload that matches the wire format the firmware
/// already parses (see daemon/payloads.py:build_minimax_usage_payload).
/// </summary>
public sealed class ProviderPollerService
{
    private readonly ILogger<ProviderPollerService> _log;

    public ProviderPollerService(ILogger<ProviderPollerService> log)
    {
        _log = log;
    }

    /// <summary>Fetch fresh usage data. Returns null on any error.</summary>
    public Task<UsagePayload?> PollAsync(ProviderId provider, CancellationToken ct)
        => provider switch
        {
            ProviderId.Minimax => PollMinimaxAsync(ct),
            // ProviderId.Claude, .Codex, .OpenRouter, .Zen, .Go, .Deepseek →
            // land in M9 once the OAuth / web-scraping strategies ship.
            _ => Task.FromResult<UsagePayload?>(null),
        };

    private Task<UsagePayload?> PollMinimaxAsync(CancellationToken ct)
    {
        // 1. HTTPS GET https://api.minimax.io/v1/token_plan/remains
        //    Authorization: Bearer <DPAPI-decrypted key from DpapiSecretStore>
        // 2. Parse response.data.model_remains[0]
        //    (same scoring / chat_score logic as
        //     daemon/payloads.py:build_minimax_usage_payload)
        // 3. Build UsagePayload { p, top, bottom, status, ... }
        // 4. Return — caller hands it to BleLinkService which writes RX char
        throw new NotImplementedException("MiniMax poll lands in M3");
    }
}
