using System.Diagnostics;
using System.IO;
using System.Windows;
using Clawmeter.UI.Services;

namespace Clawmeter.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private void OnOpenClicked(object sender, RoutedEventArgs e) => Show();

    private void OnQuitClicked(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private async void OnBrightness25(object sender, RoutedEventArgs e)  => await SetBrightnessAsync(25);
    private async void OnBrightness50(object sender, RoutedEventArgs e)  => await SetBrightnessAsync(50);
    private async void OnBrightness75(object sender, RoutedEventArgs e)  => await SetBrightnessAsync(75);
    private async void OnBrightness100(object sender, RoutedEventArgs e) => await SetBrightnessAsync(100);

    private async Task SetBrightnessAsync(int pct)
    {
        try
        {
            await App.Service.SendAsync(Clawmeter.Shared.IpcActions.SetBrightness, pct);
            await RefreshStatusAsync();
        }
        catch (Exception ex) { StatusText.Text = "Error: " + ex.Message; }
    }

    private async void OnProviderMinimax(object sender, RoutedEventArgs e)   => await SetProviderAsync(Clawmeter.Shared.ProviderId.Minimax);
    private async void OnProviderClaude(object sender, RoutedEventArgs e)   => await SetProviderAsync(Clawmeter.Shared.ProviderId.Claude);
    private async void OnProviderCodex(object sender, RoutedEventArgs e)    => await SetProviderAsync(Clawmeter.Shared.ProviderId.Codex);
    private async void OnProviderOpenRouter(object sender, RoutedEventArgs e) => await SetProviderAsync(Clawmeter.Shared.ProviderId.OpenRouter);

    private async Task SetProviderAsync(Clawmeter.Shared.ProviderId id)
    {
        try
        {
            await App.Service.SendAsync(Clawmeter.Shared.IpcActions.SetProvider, id.ToString());
            await RefreshStatusAsync();
        }
        catch (Exception ex) { StatusText.Text = "Error: " + ex.Message; }
    }

    private async void OnPollNowClicked(object sender, RoutedEventArgs e)
    {
        try { await App.Service.SendAsync(Clawmeter.Shared.IpcActions.PollNow); }
        catch (Exception ex) { StatusText.Text = "Error: " + ex.Message; }
    }

    private void OnOpenLogClicked(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clawmeter", "logs");
        if (Directory.Exists(logDir))
            Process.Start("explorer.exe", $"\"{logDir}\"");
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var status = await App.Service.GetStatusAsync();
            ServiceStatus.Text    = status.Connected ? "● Connected" : "○ Disconnected";
            ProviderText.Text      = status.Provider;
            BrightnessText.Text    = status.BrightnessPct + "%";
            StatusText.Text        = status.DaemonLive ? "Daemon live" : "Daemon silent (fallback?)";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Service not running — " + ex.Message;
        }
    }
}
