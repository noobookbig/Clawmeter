using Clawmeter.Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clawmeter.Service;

/// <summary>
/// Service entrypoint. Two modes:
///   * default / --service  → register as a Windows Service (sc-managed)
///   * --console             → run as a foreground process (for debugging)
/// Both paths share the same Generic Host wiring.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var isConsole = args.Any(a =>
            a.Equals("--console", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-c", StringComparison.OrdinalIgnoreCase));

        var builder = Host.CreateApplicationBuilder(args);

        LogConfig.Configure(builder.Logging);

        builder.Services
            .AddSingleton<BleLinkService>()
            .AddSingleton<ProviderPollerService>()
            .AddSingleton<BrightnessController>()
            .AddSingleton<ConfigService>()
            .AddSingleton<DpapiSecretStore>()
            .AddSingleton<NamedPipeServer>()
            .AddHostedService<ClawmeterHostedService>();

        var host = builder.Build();

        if (isConsole)
        {
            // Debug / dev mode — run in foreground
            await host.RunAsync();
            return 0;
        }

        // Production: hand off to Service Control Manager
        await host.RunAsServiceAsync();
        return 0;
    }
}
