using System.Text.Json;
using Clawmeter.Service.Infra;
using Clawmeter.Shared;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service.Services;

/// <summary>
/// Configuration + secrets. Replaces daemon/config.py:
///   * provider / brightness / pollInterval — plain JSON at
///     %APPDATA%\Clawmeter\config.json
///   * MiniMax API key — DPAPI-encrypted binary at
///     %APPDATA%\Clawmeter\secrets\minimax.key
/// Live reload via FileSystemWatcher — emits ConfigChanged so the
/// ProviderPollerService can pick up the new provider without a restart.
/// </summary>
public sealed class ConfigService : IDisposable
{
    private const string ConfigDir = @"Clawmeter";
    private const string ConfigFile = "config.json";
    private const string SecretsDir = "secrets";
    private const string MiniMaxKeyFile = "minimax.key";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };

    private readonly ILogger<ConfigService> _log;
    private readonly DpapiSecretStore _dpapi;
    private readonly string _configPath;
    private readonly string _miniMaxKeyPath;
    private ServiceConfig _current = new();
    private FileSystemWatcher? _watcher;

    public ConfigService(ILogger<ConfigService> log, DpapiSecretStore dpapi)
    {
        _log = log;
        _dpapi = dpapi;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, ConfigDir);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, SecretsDir));
        _configPath    = Path.Combine(dir, ConfigFile);
        _miniMaxKeyPath = Path.Combine(dir, SecretsDir, MiniMaxKeyFile);
    }

    public event EventHandler<ServiceConfig>? ConfigChanged;

    public ServiceConfig Current => _current;

    public async Task<ServiceConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                await using var fs = File.OpenRead(_configPath);
                var cfg = await JsonSerializer.DeserializeAsync<ServiceConfig>(fs, JsonOpts);
                if (cfg is not null) _current = cfg;
            }
            else
            {
                await SaveAsync(_current);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load config — using defaults");
        }
        StartWatcher();
        return _current;
    }

    public async Task SaveAsync(ServiceConfig cfg)
    {
        try
        {
            _current = cfg;
            await using var fs = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(fs, cfg, JsonOpts);
            ConfigChanged?.Invoke(this, cfg);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to save config");
        }
    }

    public string? GetMiniMaxApiKey()
    {
        if (!File.Exists(_miniMaxKeyPath)) return null;
        try
        {
            var ciphertext = File.ReadAllBytes(_miniMaxKeyPath);
            var plaintext = _dpapi.Unprotect(ciphertext);
            return System.Text.Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to decrypt MiniMax API key");
            return null;
        }
    }

    public void SetMiniMaxApiKey(string key)
    {
        try
        {
            var plaintext = System.Text.Encoding.UTF8.GetBytes(key);
            var ciphertext = _dpapi.Protect(plaintext);
            File.WriteAllBytes(_miniMaxKeyPath, ciphertext);
            _log.LogInformation("MiniMax API key saved (DPAPI, {Bytes} bytes ciphertext)", ciphertext.Length);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to encrypt/save MiniMax API key");
        }
    }

    private void StartWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath)!, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += async (_, _) =>
            {
                await Task.Delay(150);
                try { await LoadAsync(); ConfigChanged?.Invoke(this, _current); }
                catch (Exception ex) { _log.LogWarning(ex, "config reload failed"); }
            };
        }
        catch (Exception ex) { _log.LogWarning(ex, "config file watcher not started"); }
    }

    public void Dispose() => _watcher?.Dispose();
}
