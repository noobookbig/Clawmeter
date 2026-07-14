using System.Diagnostics;
using System.IO;
using System.Windows;
using Clawmeter.UI.Services;
using Microsoft.Win32;
using Clawmeter.Shared;

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
        try { await App.Service.SendAsync(Clawmeter.Shared.IpcActions.SetBrightness, pct); }
        catch (Exception ex) { StatusText.Text = "Error: " + ex.Message; }
    }

    private async void OnProviderMinimax(object sender, RoutedEventArgs e)   => await SetProviderAsync("minimax");
    private async void OnProviderClaude(object sender, RoutedEventArgs e)   => await SetProviderAsync("claude");
    private async void OnProviderCodex(object sender, RoutedEventArgs e)    => await SetProviderAsync("codex");
    private async void OnProviderOpenRouter(object sender, RoutedEventArgs e) => await SetProviderAsync("openrouter");

    private async Task SetProviderAsync(string id)
    {
        try { await App.Service.SendAsync(Clawmeter.Shared.IpcActions.SetProvider, id); }
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
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Clawmeter", "logs");
        if (Directory.Exists(logDir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logDir}\"")
                { UseShellExecute = true });
    }

    private async void OnSetApiKeyClicked(object sender, RoutedEventArgs e)
    {
        var input = new InputDialog
        {
            Title  = "Set MiniMax API key",
            Label  = "API key (sk-cp-…) — encrypted with DPAPI, stored in %APPDATA%\\Clawmeter\\secrets",
            IsSecret = true,
        };
        if (input.ShowDialog() != true) return;

        try
        {
            await App.Service.SendAsync("set-apikey", input.Value);
            StatusText.Text = "API key saved.";
        }
        catch (Exception ex) { StatusText.Text = "Error: " + ex.Message; }
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var status = await App.Service.GetStatusAsync();
            ServiceStatus.Text   = status.Connected ? "● Connected" : "○ Disconnected";
            ProviderText.Text     = status.Provider;
            BrightnessText.Text   = status.BrightnessPct + "%";
            StatusText.Text       = status.DaemonLive ? "Daemon live" : "Daemon silent (fallback?)";
            FooterText.Text       = $"Last sync: {(status.LastSync?.ToLocalTime().ToString("u") ?? "—")}    Service: Clawmeter";
        }
        catch (Exception ex)
        {
            ServiceStatus.Text = "○ Disconnected";
            StatusText.Text     = "Service not running — " + ex.Message;
            FooterText.Text     = "Run:  Clawmeter.Installer install   (as Administrator)";
        }
    }
}

/// <summary>
/// Simple input dialog used for the API-key prompt. Encapsulated here so
/// we don't pull in a whole dialog framework (M6 keeps deps lean).
/// </summary>
public sealed class InputDialog : Window
{
    public string Value { get; private set; } = "";
    public bool IsSecret { get; set; }

    public InputDialog()
    {
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
    }

    public new bool? ShowDialog()
    {
        var ok = new Button { Content = "OK",     Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };

        var label = new System.Windows.Controls.TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
        };
        if (IsSecret) label.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new System.Windows.Data.Binding("Label") { Source = this });

        var input = new System.Windows.Controls.TextBox
        {
            Margin = new Thickness(0, 0, 0, 12),
        };
        if (IsSecret) input.Visibility = Visibility.Visible; else input.Visibility = Visibility.Visible;
        if (IsSecret) input.FontFamily = new System.Windows.Media.FontFamily("Consolas");

        ok.Click += (_, _) => { Value = input.Text; DialogResult = true; };

        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(label);
        stack.Children.Add(input);
        stack.Children.Add(buttons);
        Content = stack;

        return base.ShowDialog();
    }
}
