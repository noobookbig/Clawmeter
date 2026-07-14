using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using Clawmeter.Shared;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;

namespace Clawmeter.Service.Services;

/// <summary>
/// BLE Central-role client. Replaces the Python daemon's bleak loop in
/// daemon/claude_usage_daemon_windows.py:
///   1. Watcher scans for the device advertized as "Clawdmeter"
///   2. Connect, discover the custom GATT service
///   3. Subscribe to TX (ack/nack) and REQ (refresh request) chars
///   4. On REQ, fire the poll strategy, build UsagePayload, write to RX
///   5. Reconnect on broken pipe (WinRT's transient GATT errors)
/// Uses pure Windows.Devices.Bluetooth (UWP / WinRT) — no Bleak equivalent
/// needed in C#.
/// </summary>
public sealed class BleLinkService : IDisposable
{
    private const string DeviceName = "Clawdmeter";
    private const string BleAddressEnv = "CLAWDMETER_BLE_ADDRESS";
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<BleLinkService> _log;
    private readonly ProviderPollerService _poller;
    private readonly BrightnessController _brightness;
    private readonly ConfigService _config;

    private BluetoothLEDevice? _device;
    private GattCharacteristic? _rxChar;
    private GattCharacteristic? _txChar;
    private GattCharacteristic? _reqChar;
    private CancellationTokenSource? _cts;
    private Task? _watcherTask;

    public event EventHandler<BleStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<UsagePayload>? PayloadReady;

    public bool IsConnected { get; private set; }

    public BleLinkService(
        ILogger<BleLinkService> log,
        ProviderPollerService poller,
        BrightnessController brightness,
        ConfigService config)
    {
        _log = log;
        _poller = poller;
        _brightness = brightness;
        _config = config;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _watcherTask = Task.Run(() => WatcherLoopAsync(_cts.Token), _cts.Token);
        _log.LogInformation("BLE link service started");
        await Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        return _watcherTask ?? Task.CompletedTask;
    }

    public async Task<bool> WritePayloadAsync(UsagePayload payload, CancellationToken ct)
    {
        var rx = _rxChar;
        if (rx is null)
        {
            _log.LogDebug("RX char not yet available");
            return false;
        }
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var data = Encoding.UTF8.GetBytes(json);
            var status = await rx.WriteValueAsync(
                BluetoothCacheMode.Uncached,
                Windows.Storage.Streams.IBufferExtensions.AsBuffer(data).ToArray()
                    .AsBuffer());
            return status == GattCommunicationStatus.Success;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "BLE RX write failed — link may be down");
            return false;
        }
    }

    public async Task<bool> SetBrightnessPctAsync(int pct, CancellationToken ct)
    {
        var rx = _rxChar;
        if (rx is null) return false;
        try
        {
            var data = Encoding.UTF8.GetBytes($"{{\"brightness\":{pct}}}");
            var status = await rx.WriteValueAsync(
                BluetoothCacheMode.Uncached,
                Windows.Storage.Streams.IBufferExtensions.AsBuffer(data).ToArray()
                    .AsBuffer());
            return status == GattCommunicationStatus.Success;
        }
        catch (Exception ex) { _log.LogWarning(ex, "brightness write failed"); return false; }
    }

    private async Task WatcherLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await EnsureConnectedAsync(ct))
                {
                    _log.LogInformation("BLE disconnected — retrying in 5s");
                    IsConnected = false;
                    StatusChanged?.Invoke(this, new(false, "disconnected"));
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
                StatusChanged?.Invoke(this, new(true, "connected"));
                IsConnected = true;
                _log.LogInformation("BLE connected to {Name}", _device!.Name);

                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "BLE watcher error — sleeping 5s before retry");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        // Try cached address first
        var addr = Environment.GetEnvironmentVariable(BleAddressEnv);
        var device = await TryConnectByAddressAsync(addr, ct)
                    ?? await ScanAndConnectAsync(ct);

        if (device is null) return false;
        _device = device;

        var service = device.GetGattServices(Clawmeter.Shared.BleProtocol.ServiceUuid)
            .FirstOrDefault();
        if (service is null)
        {
            _log.LogWarning("GATT service {Uuid} not found", Clawmeter.Shared.BleProtocol.ServiceUuid);
            device.Dispose();
            _device = null;
            return false;
        }

        var chars = service.GetAllCharacteristics();
        _rxChar = chars.FirstOrDefault(c => c.Uuid == Clawmeter.Shared.BleProtocol.RxCharUuid);
        _txChar = chars.FirstOrDefault(c => c.Uuid == Clawmeter.Shared.BleProtocol.TxCharUuid);
        _reqChar = chars.FirstOrDefault(c => c.Uuid == Clawmeter.Shared.BleProtocol.ReqCharUuid);

        if (_txChar is not null) await _txChar.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (_reqChar is not null) await _reqChar.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);

        _txChar!.ValueChanged += OnTxChanged;
        _reqChar!.ValueChanged += OnReqChanged;
        return true;
    }

    private async Task<BluetoothLEDevice?> TryConnectByAddressAsync(string? addr, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(addr)) return null;
        try
        {
            var fromAddress = await BluetoothLEDevice.FromBluetoothAddressAsync(ulong.Parse(addr, System.Globalization.NumberStyles.HexNumber));
            await fromAddress.RequestAccessAsync();
            return fromAddress;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "cached address {Addr} failed", addr);
            return null;
        }
    }

    private async Task<BluetoothLEDevice?> ScanAndConnectAsync(CancellationToken ct)
    {
        try
        {
            using var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active,
            };
            var tcs = new TaskCompletionSource<ulong?>();
            watcher.Received += (w, e) =>
            {
                if (e.Advertisement.LocalName == DeviceName)
                {
                    tcs.TrySetResult(e.BluetoothAddress);
                }
            };
            watcher.Start();
            using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            scanCts.CancelAfter(ScanTimeout);
            using var reg = scanCts.Token.Register(() => tcs.TrySetResult(null));
            var addr = await tcs.Task;
            watcher.Stop();
            if (addr is null) return null;

            var dev = await BluetoothLEDevice.FromBluetoothAddressAsync(addr.Value);
            await dev.RequestAccessAsync();
            return dev;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "BLE scan failed");
            return null;
        }
    }

    private async void OnTxChanged(GattCharacteristic sender, GattValueChangedEventArgs e)
    {
        try
        {
            var buf = Windows.Storage.Streams.DataReader.FromBuffer(e.CharacteristicValue);
            var len = buf.UnconsumedBufferLength;
            var bytes = new byte[len];
            buf.ReadBytes(bytes);
            var s = Encoding.UTF8.GetString(bytes);
            _log.LogDebug("TX: {S}", s);
            // Update last-data age so the C-side watchdog stays quiet
        }
        catch (Exception ex) { _log.LogWarning(ex, "TX notification parse failed"); }
    }

    private async void OnReqChanged(GattCharacteristic sender, GattValueChangedEventArgs e)
    {
        _log.LogInformation("REFRESH requested by firmware");
        try
        {
            var cfg = _config.Current;
            var payload = await _poller.PollAsync(
                Enum.TryParse<ProviderId>(cfg.Provider, true, out var p) ? p : ProviderId.Minimax,
                CancellationToken.None);
            if (payload is null)
            {
                _log.LogWarning("poll returned null — nothing to send");
                return;
            }
            // Apply daemon-side brightness override before sending
            var withB = payload with { BrightnessPct = cfg.BrightnessPct };
            await WritePayloadAsync(withB, CancellationToken.None);
            PayloadReady?.Invoke(this, withB);
        }
        catch (Exception ex) { _log.LogWarning(ex, "REQ poll+write failed"); }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _device?.Dispose(); } catch { /* swallow */ }
    }
}

public sealed record BleStatusChangedEventArgs(bool Connected, string Status);
