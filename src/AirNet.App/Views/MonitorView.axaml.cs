using Avalonia.Controls;
using Avalonia.Interactivity;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        InitializeComponent();
    }

    private async void OnExportReportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel viewModel && TopLevel.GetTopLevel(this) is { } top)
        {
            await viewModel.Shell.ExportReportWithPickerAsync(top);
        }
    }

    private async void OnCopyDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MonitorViewModel viewModel)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            viewModel.Shell.StatusMessage = "剪贴板不可用";
            return;
        }

        await clipboard.SetTextAsync(viewModel.Shell.BuildDiagnosticText());
        viewModel.Shell.StatusMessage = "诊断信息已复制到剪贴板";
    }

    private void OnFindingActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FindingRow row }
            && DataContext is MonitorViewModel viewModel
            && row.ActionTarget is { } target)
        {
            viewModel.Shell.NavigateCommand.Execute(target);
        }
    }
}
