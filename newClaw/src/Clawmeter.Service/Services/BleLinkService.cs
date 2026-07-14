using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// BLE Central-role client. Replaces the Python daemon's bleak loop:
///   1. Watcher scans for the device advertized as "Clawdmeter"
///   2. Connect, discover the custom GATT service
///   3. Subscribe to the TX characteristic (ack/nack feedback)
///   4. When the firmware fires REQ, poll MiniMax, build a UsagePayload
///      matching the Python daemon's shape, and write to RX.
///
/// The actual BLE bits (Windows.Devices.Bluetooth) land in M2. This
/// file is the orchestrator skeleton.
/// </summary>
public sealed class BleLinkService
{
    private readonly ILogger<BleLinkService> _log;

    public BleLinkService(ILogger<BleLinkService> log)
    {
        _log = log;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync()                  => Task.CompletedTask;
}
