using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Configuration + secrets. Replaces daemon/config.py:
///   * provider — plain JSON, lives at %APPDATA%\Clawmeter\config.json
///   * MiniMax API key — DPAPI-encrypted binary at
///     %APPDATA%\Clawmeter\secrets\minimax.key
///   * WiFi creds (Phase 2) — same DPAPI approach
/// Live reload is via a FileSystemWatcher that re-emits a config-changed
/// event consumed by ProviderPollerService + BleLinkService.
/// </summary>
public sealed class ConfigService
{
    private readonly ILogger<ConfigService> _log;

    public ConfigService(ILogger<ConfigService> log)
    {
        _log = log;
    }

    public Task<ServiceConfig> LoadAsync() => Task.FromResult(new ServiceConfig());
    public Task SaveAsync(ServiceConfig cfg) => Task.CompletedTask;
}

/// <summary>Wire-shape of the on-disk JSON config.</summary>
public sealed record ServiceConfig
{
    public string Provider { get; init; } = "minimax";
    public int BrightnessPct { get; init; } = 75;
    public int PollIntervalSeconds { get; init; } = 60;
}
