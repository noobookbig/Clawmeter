using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Clawmeter.Service.Infra;

/// <summary>
/// Serilog setup. The default logger host replaces the empty MS logger, so
/// every ILogger<T> in the project emits structured events. Sinks:
///   * RollingFile      %APPDATA%\Clawmeter\logs\daemon-.log  (daily, 14-day)
///   * EventLog         Service channel "Clawmeter"
///   * Console          in --console mode only
/// </summary>
public static class LogConfig
{
    public static void Configure(ILoggingBuilder builder)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clawmeter", "logs");
        Directory.CreateDirectory(logDir);

        var logCfg = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Clawmeter.Service")
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(logDir, "daemon-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true);

        if (Environment.GetCommandLineArgs().Any(a =>
                a.Equals("--console", StringComparison.OrdinalIgnoreCase)))
        {
            logCfg = logCfg.WriteTo.Console();
        }

        Log.Logger = logCfg.CreateLogger();
        builder.ClearProviders().AddSerilog(dispose: true);
    }
}
