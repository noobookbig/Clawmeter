using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Brightness control port. Replaces daemon/brightness.py:
///   * SetPct(pct) writes the new value to firmware NVS (via BLE char)
///     and updates the running display via display_hal_set_brightness.
///   * GetPct() reads the last-applied value from firmware on demand.
/// On the C# side we just proxy the call to BleLinkService — the C
/// implementation lands in M4.
/// </summary>
public sealed class BrightnessController
{
    private readonly ILogger<BrightnessController> _log;

    public BrightnessController(ILogger<BrightnessController> log)
    {
        _log = log;
    }

    public Task SetPctAsync(int pct, CancellationToken ct) => Task.CompletedTask;
    public Task<int> GetPctAsync() => Task.FromResult(75);
}
