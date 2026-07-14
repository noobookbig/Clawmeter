using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Clawmeter.Shared;

namespace Clawmeter.Service.Services;

/// <summary>
/// Top-level BackgroundService. Wires the BleLink + ProviderPoller
/// + Config + NamedPipe. Drives the poll cycle every N seconds (or on
/// REQ notification from the firmware).
/// </summary>
public sealed class ClawmeterHostedService : BackgroundService
{
    private readonly ILogger<ClawmeterHostedService> _log;
    private readonly ConfigService _config;
    private readonly BleLinkService _ble;
    private readonly ProviderPollerService _poller;
    private readonly BrightnessController _brightness;
    private readonly NamedPipeServer _pipe;

    public ClawmeterHostedService(
        ILogger<ClawmeterHostedService> log,
        ConfigService config,
        BleLinkService ble,
        ProviderPollerService poller,
        BrightnessController brightness,
        NamedPipeServer pipe)
    {
        _log = log;
        _config = config;
        _ble = ble;
        _poller = poller;
        _brightness = brightness;
        _pipe = pipe;

        _ble.PayloadReady += (_, payload) =>
            _log.LogDebug("Payload sent to BLE: top={Top} bottom={Bot}",
                payload.Top.Pct, payload.Bottom.Pct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Clawmeter service starting");
        var cfg = await _config.LoadAsync();
        _log.LogInformation("Config loaded: provider={Provider} brightness={Pct}",
            cfg.Provider, cfg.BrightnessPct);

        await _ble.StartAsync(stoppingToken);
        await _pipe.StartAsync(stoppingToken);

        // Wait for BLE to connect (caller drives via the PayloadReady event).
        // Periodically send a payload even if the firmware didn't ask — the
        // 60s tick keeps the display fresh.
        var pollInterval = TimeSpan.FromSeconds(Math.Max(15, cfg.PollIntervalSeconds));
        using var tick = new PeriodicTimer(pollInterval);

        while (await tick.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!_ble.IsConnected) continue;
                var payload = await _poller.PollAsync(
                    Enum.TryParse<ProviderId>(cfg.Provider, true, out var p) ? p : ProviderId.Minimax,
                    stoppingToken);
                if (payload is null) continue;
                var withB = payload with { BrightnessPct = cfg.BrightnessPct };
                await _ble.WritePayloadAsync(withB, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "poll cycle failed — will retry next tick");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("Clawmeter service stopping");
        await _ble.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}

public static class BleStatusConnectedExtensions
{
    public static bool StatusConnected(this BleLinkService ble) => ble.IsConnected;
}
