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
}
