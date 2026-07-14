namespace BleProvisioner;

/// <summary>
/// CLI: write SSID + WiFi PSK + MiniMax API key into the ESP32 firmware NVS
/// over BLE. Replaces the legacy daemon/BLE push path.
///
/// Usage:
///   BleProvisioner ssid &lt;name&gt; psk &lt;password&gt; minimax-key &lt;key&gt; [device &lt;name-or-addr&gt;]
///
/// The actual BLE write lives in M8. This file is the parameter parser +
/// entry point; the heavy lifting is in Clawmeter.Service's BleLinkService
/// once the daemon service is up.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  BleProvisioner ssid <name> psk <password> minimax-key <key> [device <name>]");
            return 0;
        }
        // TODO(M8): parse args, push via BleLinkService write NVS chars
        throw new NotImplementedException("BleProvisioner lands in M8");
    }
}
