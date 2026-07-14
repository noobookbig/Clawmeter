using System.Text.Json.Serialization;

namespace Clawmeter.Shared;

/// <summary>
/// Top-level payload sent over BLE to the ESP32 firmware. The shape MUST
/// match the JSON the Python daemon's build_windowed_payload produces today
/// (see daemon/payloads.py). The firmware parses this directly with
/// usage_parse_json() — do not rename fields without updating the C parser.
/// </summary>
public sealed record UsagePayload
{
    [JsonPropertyName("p")]          public string Provider { get; init; } = "minimax";
    [JsonPropertyName("plan_type")]  public string PlanType { get; init; } = "subscription";
    [JsonPropertyName("mode")]       public string Mode { get; init; } = "window";
    [JsonPropertyName("status")]     public string Status { get; init; } = "ok";
    [JsonPropertyName("ok")]         public bool   Ok { get; init; } = true;

    [JsonPropertyName("top")]        public required PanelPayload Top { get; init; }
    [JsonPropertyName("bottom")]     public required PanelPayload Bottom { get; init; }
    [JsonPropertyName("s")]          public int?    SPct { get; init; }   // legacy alias
    [JsonPropertyName("sr")]         public int?    SResetMins { get; init; }
    [JsonPropertyName("w")]          public int?    WPct { get; init; }
    [JsonPropertyName("wr")]         public int?    WResetMins { get; init; }

    [JsonPropertyName("brightness")] public int?    BrightnessPct { get; init; }
}

/// <summary>
/// Per-window panel data (current + weekly quota percentages + reset minutes).
/// </summary>
public sealed record PanelPayload
{
    [JsonPropertyName("label")]      public string Label { get; init; } = "";
    [JsonPropertyName("kind")]       public string Kind { get; init; } = "window_short";
    [JsonPropertyName("pct")]        public int    Pct { get; init; }
    [JsonPropertyName("reset_mins")] public int    ResetMins { get; init; }
    [JsonPropertyName("has_reset")]  public bool   HasReset { get; init; } = true;
}
