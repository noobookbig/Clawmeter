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
        var serviceExe = Path.Combine(AppContext.BaseDirectory, "Clawmeter.Service.exe");
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Clawmeter", "logs");
        Directory.CreateDirectory(logDir);

        if (!File.Exists(serviceExe))
            return Fail($"service binary not found: {serviceExe}");

        var binPath = $"\"{serviceExe}\"";
        var display = "Clawmeter Usage Monitor";
        var description = "BLE bridge to ESP32 + MiniMax usage polling. Service-mode (no console).";

        // Register + start. Start=auto means the SCM auto-restarts on crash.
        Run("sc.exe", $"create ClawmeterSvc binPath= {binPath} start= auto DisplayName= \"{display}\" obj= LocalSystem");
        Run("sc.exe", $"description  ClawmeterSvc \"{description}\"");
        Run("sc.exe",  "failure ClawmeterSvc reset= 5 seconds actions= restart/5000/restart/5000/restart/5000");
        Run("sc.exe",  "start ClawmeterSvc");
        Console.WriteLine("Clawmeter service installed and started.");
        Console.WriteLine("Log directory: " + logDir);
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
            Verb                   = "runas",  // UAC: request elevation if needed
            CreateNoWindow         = true,
        };
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            if (redirectOutput) Console.WriteLine(p.StandardOutput.ReadToEnd());
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC cancelled
            return Fail("admin elevation required to manage Windows service");
        }
    }

    private static int Fail(string msg)
    {
        Console.Error.WriteLine("error: " + msg);
        return 1;
    }
}
