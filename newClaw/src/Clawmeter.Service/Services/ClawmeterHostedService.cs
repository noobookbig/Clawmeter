using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Top-level BackgroundService. Owns the run-loop: kick off BleLink,
/// start the named-pipe IPC server, and dispatch each poll cycle. The
/// actual logic lives in BleLinkService / ProviderPollerService /
/// BrightnessController; this class wires them together and surfaces
/// lifecycle events.
/// </summary>
public sealed class ClawmeterHostedService : BackgroundService
{
    private readonly ILogger<ClawmeterHostedService> _log;
    private readonly BleLinkService _ble;
    private readonly NamedPipeServer _pipe;

    public ClawmeterHostedService(
        ILogger<ClawmeterHostedService> log,
        BleLinkService ble,
        NamedPipeServer pipe)
    {
        _log = log;
        _ble = ble;
        _pipe = pipe;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Clawmeter service starting");
        await _ble.StartAsync(stoppingToken);
        await _pipe.StartAsync(stoppingToken);
        // Block until shutdown is requested
        await Task.Delay(Timeout.Infinite, stoppingToken)
            .ContinueWith(_ => { }, TaskScheduler.Default);
        _log.LogInformation("Clawmeter service stopping");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("Clawmeter service stop requested");
        await base.StopAsync(cancellationToken);
    }
}
