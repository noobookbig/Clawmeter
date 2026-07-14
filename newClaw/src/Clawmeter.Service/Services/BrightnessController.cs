using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Brightness control port. Replaces daemon/brightness.py — the firmware
/// accepts a top-level "brightness": N in the JSON payload, so we apply
/// the override before each write instead of holding a separate handle.
/// </summary>
public sealed class BrightnessController
{
    private readonly ILogger<BrightnessController> _log;
    private readonly ConfigService _config;

    public BrightnessController(ILogger<BrightnessController> log, ConfigService config)
    {
        _log = log;
        _config = config;
    }

    public Task SetPctAsync(int pct, CancellationToken ct)
    {
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;
        _log.LogInformation("Brightness set: {Pct}%", pct);
        // Update config so the next poll picks it up; the actual firmware
        // write happens in BleLinkService via PayloadReady.
        return _config.SaveAsync(_config.Current with { BrightnessPct = pct });
    }

    public Task<int> GetPctAsync()
    {
        return Task.FromResult(_config.Current.BrightnessPct);
    }
}
