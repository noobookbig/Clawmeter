using System.Text.Json;
using Clawmeter.Shared;
using Xunit;

namespace Clawmeter.Tests;

/// <summary>
/// Round-trip test for the wire JSON shape — anything we serialize here
/// is what the firmware's usage_parse_json() will see. Catches drift early.
/// </summary>
public sealed class BleProtocolParserTests
{
    [Fact]
    public void UsagePayload_RoundTrip_PreservesAllFields()
    {
        var payload = new UsagePayload
        {
            Provider  = "minimax",
            PlanType  = "subscription",
            Mode      = "window",
            Status    = "ok",
            Ok        = true,
            Top       = new PanelPayload { Label = "Current", Kind = "window_short", Pct = 42,  ResetMins = 120,   HasReset = true },
            Bottom    = new PanelPayload { Label = "Weekly",  Kind = "window_long",  Pct = 58,  ResetMins = 4320,  HasReset = true },
            BrightnessPct = 75,
        };

        var json   = JsonSerializer.Serialize(payload);
        var parsed = JsonSerializer.Deserialize<UsagePayload>(json);

        Assert.NotNull(parsed);
        Assert.Equal(payload.Provider,       parsed!.Provider);
        Assert.Equal(payload.PlanType,       parsed.PlanType);
        Assert.Equal(payload.Mode,           parsed.Mode);
        Assert.Equal(payload.Status,         parsed.Status);
        Assert.Equal(payload.Ok,             parsed.Ok);
        Assert.Equal(payload.Top.Pct,        parsed.Top.Pct);
        Assert.Equal(payload.Top.ResetMins,  parsed.Top.ResetMins);
        Assert.Equal(payload.Bottom.Pct,     parsed.Bottom.Pct);
        Assert.Equal(payload.Bottom.ResetMins, parsed.Bottom.ResetMins);
        Assert.Equal(payload.BrightnessPct,  parsed.BrightnessPct);
    }

    [Fact]
    public void ProviderId_ToLabel_RoundTripsKnownNames()
    {
        Assert.Equal("MiniMax",    ProviderId.Minimax.ToLabel());
        Assert.Equal("Claude",     ProviderId.Claude.ToLabel());
        Assert.Equal("Codex",      ProviderId.Codex.ToLabel());
        Assert.Equal("OpenRouter", ProviderId.OpenRouter.ToLabel());
    }
}
