using Avalonia.Controls;
using Avalonia.Interactivity;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnCopyDnsCommandClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(viewModel.DnsCommandText);
        viewModel.DnsStatusText = "命令已复制，可粘贴到管理员终端执行";
    }

    /// <summary>
    /// The bundle needs a TopLevel for the save dialog, which only the view has; the view model
    /// still owns the work and the status text.
    /// </summary>
    private async void OnExportDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && TopLevel.GetTopLevel(this) is { } top)
        {
            await viewModel.ExportDiagnosticsAsync(top);
        }
    }
}
