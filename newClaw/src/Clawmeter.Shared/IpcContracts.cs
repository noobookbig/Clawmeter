using System.Text.Json.Serialization;

namespace Clawmeter.Shared;

/// <summary>
/// Named-pipe request record. Marshalled as JSON over \\.\pipe\Clawmeter.
/// Local-only — restricted to current user via pipe ACLs.
/// </summary>
public sealed record IpcRequest
{
    [JsonPropertyName("action")]  public required string Action { get; init; }
    [JsonPropertyName("payload")] public          object? Payload { get; init; }
}

/// <summary>
/// Named-pipe response record. Ok=false means the call failed; Error
/// contains a human-readable string for the UI to surface.
/// </summary>
public sealed record IpcResponse
{
    [JsonPropertyName("ok")]    public bool   Ok    { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("data")]  public object? Data  { get; init; }
}

/// <summary>
/// Standard IPC actions. Both Service and UI agree on these strings.
/// </summary>
public static class IpcActions
{
    public const string Start         = "start";
    public const string Stop          = "stop";
    public const string SetBrightness = "set-brightness";
    public const string SetProvider   = "set-provider";
    public const string PollNow       = "poll-now";
    public const string GetStatus     = "get-status";
}

/// <summary>Returned by GetStatus; the UI displays this in its main panel.</summary>
public sealed record ServiceStatus
{
    public bool Connected { get; init; }
    public string Provider { get; init; } = "";
    public int BrightnessPct { get; init; }
    public DateTime? LastSync { get; init; }
    public bool DaemonLive { get; init; }
}
