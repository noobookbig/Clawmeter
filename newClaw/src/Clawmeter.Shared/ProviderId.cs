namespace Clawmeter.Shared;

/// <summary>
/// Provider identifiers shared between firmware, daemon service, and UI.
/// Numeric values MUST match the firmware's USAGE_PROVIDER_* enum
/// (data.h) so the daemon can write the same JSON the firmware already parses.
/// </summary>
public enum ProviderId : byte
{
    Unknown = 0,
    Claude,
    Codex,
    OpenRouter,
    Zen,
    Go,
    Deepseek,
    Minimax,   // = 7 — same as USAGE_PROVIDER_MINIMAX
}

/// <summary>Human-readable label for the UI / logs.</summary>
public static class ProviderIdExtensions
{
    public static string ToLabel(this ProviderId id) => id switch
    {
        ProviderId.Claude     => "Claude",
        ProviderId.Codex      => "Codex",
        ProviderId.OpenRouter => "OpenRouter",
        ProviderId.Zen        => "Zen",
        ProviderId.Go         => "OpenCode Go",
        ProviderId.Deepseek   => "DeepSeek",
        ProviderId.Minimax    => "MiniMax",
        _                     => "Unknown",
    };
}
