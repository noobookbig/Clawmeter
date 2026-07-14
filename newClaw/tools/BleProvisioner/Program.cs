using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Clawmeter.Shared;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace BleProvisioner;

/// <summary>
/// CLI: write SSID + WiFi PSK + MiniMax API key into the ESP32 firmware NVS
/// over BLE. Replaces the legacy daemon/ble push path.
///
/// Usage:
///   BleProvisioner ssid &lt;name&gt; psk &lt;password&gt; minimax-key &lt;key&gt; [device &lt;name&gt;]
///
/// Walks the same BLE path as the service: scan → connect → discover the
/// custom GATT service → write to RX with a {"wifi":{...}, "key":...} payload.
/// Firmware (BleProvisioner firmware side, landed in the C-side M8) reads
/// the JSON, stores SSID/PSK in "clawdmeter_wifi" and the key in
/// "clawdmeter_minimax" NVS namespaces, then reboots.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("BleProvisioner — push SSID + API key over BLE");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  BleProvisioner ssid <name> psk <password> minimax-key <key> [device <name-or-addr>]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  BleProvisioner ssid MyWiFi psk secret minimax-key sk-cp-...");
            Console.WriteLine("  BleProvisioner ssid MyWiFi psk secret minimax-key sk-cp-... device 12:34:56:78:9A:BC");
            return 0;
        }

        var ssid     = Get(args, "ssid");
        var psk      = Get(args, "psk");
        var apiKey   = Get(args, "minimax-key");
        var deviceArg = Get(args, "device");

        if (ssid is null || psk is null || apiKey is null)
        {
            Console.Error.WriteLine("error: --ssid, --psk, --minimax-key are required");
            return 2;
        }

        var deviceName = deviceArg ?? "Clawdmeter";

        Console.WriteLine($"Scanning for '{deviceName}'...");
        var addr = await ScanAsync(deviceName);
        if (addr is null)
        {
            Console.Error.WriteLine($"error: device '{deviceName}' not found");
            return 1;
        }
        Console.WriteLine($"  found: {addr}");

        Console.WriteLine($"Connecting...");
        var dev = await BluetoothLEDevice.FromBluetoothAddressAsync(addr.Value);
        await dev.RequestAccessAsync();
        var service = dev.GetGattServices(BleProtocol.ServiceUuid).FirstOrDefault();
        if (service is null)
        {
            Console.Error.WriteLine("error: GATT service not found");
            return 1;
        }
        var rx = service.GetAllCharacteristics()
            .FirstOrDefault(c => c.Uuid == BleProtocol.RxCharUuid);
        if (rx is null)
        {
            Console.Error.WriteLine("error: RX characteristic not found");
            return 1;
        }

        var payload = new
        {
            wifi = new
            {
                ssid,
                psk,
            },
            key = apiKey,
        };
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);
        Console.WriteLine($"Pushing {data.Length} bytes: {json}");
        var status = await rx.WriteValueAsync(
            BluetoothCacheMode.Uncached,
            Windows.Storage.Streams.IBufferExtensions.AsBuffer(data).ToArray()
                .AsBuffer());
        if (status != GattCommunicationStatus.Success)
        {
            Console.Error.WriteLine($"error: write failed ({status})");
            return 1;
        }
        Console.WriteLine("Done.  Firmware will reboot and connect to WiFi.");
        dev.Dispose();
        return 0;
    }

    private static string? Get(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    private static async Task<ulong?> ScanAsync(string deviceName)
    {
        using var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active,
        };
        var tcs = new TaskCompletionSource<ulong?>();
        watcher.Received += (_, e) =>
        {
            if (e.Advertisement.LocalName == deviceName) tcs.TrySetResult(e.BluetoothAddress);
        };
        watcher.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var reg = cts.Token.Register(() => tcs.TrySetResult(null));
        return await tcs.Task;
    }
}
