using System.Windows;
using Clawmeter.UI.Services;

namespace Clawmeter.UI;

public partial class App : Application
{
    public static ServiceClient Service { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Tray icon is initialised lazily by MainWindow.
        var win = new MainWindow();
        win.Hide();   // start hidden in tray
        MainWindow = win;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Service.Dispose();
        base.OnExit(e);
    }
}
