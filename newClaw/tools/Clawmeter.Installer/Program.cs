using System.Diagnostics;

namespace Clawmeter.Installer;

/// <summary>
/// sc.exe wrapper. Installs or removes the Clawmeter Service on the local
/// machine. Run as Administrator.
///   install   — sc create + sc start
///   uninstall — sc stop + sc delete
///   status    — sc query
/// Run `Clawmeter.Installer --help` for usage.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  Clawmeter.Installer install");
            Console.WriteLine("  Clawmeter.Installer uninstall");
            Console.WriteLine("  Clawmeter.Installer status");
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "install"   => Install(),
            "uninstall" => Uninstall(),
            "status"    => Status(),
            _ => Fail($"unknown command: {args[0]}"),
        };
    }

    private static int Install()
    {
        var serviceExe = Path.Combine(
            AppContext.BaseDirectory, "Clawmeter.Service.exe");
        if (!File.Exists(serviceExe))
            return Fail($"service binary not found: {serviceExe}");

        var binPath = $"\"{serviceExe}\"";
        var display  = "Clawmeter Usage Monitor";
        Run("sc.exe", $"create ClawmeterSvc binPath= {binPath} start= auto DisplayName= \"{display}\"");
        Run("sc.exe",  "description ClawmeterSvc \"BLE bridge to ESP32 + MiniMax usage polling\"");
        Run("sc.exe",  "start ClawmeterSvc");
        return 0;
    }

    private static int Uninstall()
    {
        Run("sc.exe", "stop    ClawmeterSvc");
        Run("sc.exe", "delete  ClawmeterSvc");
        return 0;
    }

    private static int Status()
    {
        Run("sc.exe", "query ClawmeterSvc", redirectOutput: true);
        return 0;
    }

    private static int Run(string exe, string args, bool redirectOutput = false)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError  = redirectOutput,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (redirectOutput) Console.WriteLine(p.StandardOutput.ReadToEnd());
        return p.ExitCode;
    }

    private static int Fail(string msg)
    {
        Console.Error.WriteLine("error: " + msg);
        return 1;
    }
}
