using System.Text.Json;
using Clawmeter.Shared;
using Xunit;

namespace Clawmeter.Tests;

/// <summary>
/// Validates the MiniMax poll payload shape and the firmware/host enum
/// alignment. The actual HTTP / BLE calls are integration-tested in
/// BleLinkServiceTests and run on real hardware.
/// </summary>
public sealed class MiniMaxPollStrategyTests
{
    [Fact]
    public void BleProtocol_Bytes_FitExpectedShape()
    {
        // Verify that the BleProtocol record types deserialize from the
        // exact JSON shape the firmware emits. The host side must
        // produce byte-for-byte the same when serializing.
        var json = """
        {
            "p": "minimax", "mode": "window", "status": "allowed", "ok": true,
            "top":    { "label": "Current", "kind": "window_short", "pct": 80, "reset_mins": 120, "has_reset": true },
            "bottom": { "label": "Weekly",  "kind": "window_long",  "pct": 60, "reset_mins": 4320, "has_reset": true },
            "s": 80, "sr": 120, "w": 60, "wr": 4320, "brightness": 75
        }
        """;
        var payload = JsonSerializer.Deserialize<UsagePayload>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(payload);
        Assert.Equal("minimax", payload!.Provider);
        Assert.Equal(80, payload.Top.Pct);
        Assert.Equal(60, payload.Bottom.Pct);
        Assert.Equal(75, payload.BrightnessPct);
    }

    [Fact]
    public void ProviderId_Minimax_HasCorrectUnderlyingValue()
    {
        // The byte values must match the firmware's USAGE_PROVIDER_*
        // enum in data.h. If we ever change the enum order, we must update
        // the firmware in lockstep.
        Assert.Equal((byte)7, (byte)ProviderId.Minimax);
        Assert.Equal((byte)0, (byte)ProviderId.Unknown);
        Assert.Equal((byte)1, (byte)ProviderId.Claude);
    }

    [Fact]
    public void PanelPayload_Defaults_AreSafe()
    {
        var p = new PanelPayload();
        Assert.Equal("", p.Label);
        Assert.Equal("window_short", p.Kind);
        Assert.Equal(0, p.Pct);
        Assert.Equal(0, p.ResetMins);
        Assert.True(p.HasReset);
    }

    [Fact]
    public void ProviderId_ToLabel_HandlesUnknown()
    {
        Assert.Equal("Unknown", ProviderId.Unknown.ToLabel());
    }
}
