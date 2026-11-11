using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private async void OnExportReportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel viewModel || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出 AirNet 报告",
            SuggestedFileName = $"airnet-{DateTime.Now:yyyyMMdd-HHmm}",
            DefaultExtension = "html",
            FileTypeChoices =
            [
                new FilePickerFileType("HTML 报告") { Patterns = ["*.html"] },
            ],
        });

        if (file?.Path is not { } uri)
        {
            return;
        }

        await viewModel.WriteReportAsync(uri.LocalPath);
    }

    /// <summary>Keeps the custom chrome in sync with the real window state.</summary>
    public void SyncWindowState(WindowState state)
    {
        var maximized = state == WindowState.Maximized;
        MaximizeGlyph.IsVisible = !maximized;
        RestoreGlyph.IsVisible = maximized;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Host is not { } window)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize(window);
            return;
        }

        if (window.WindowState == WindowState.FullScreen)
        {
            return;
        }

        window.BeginMoveDrag(e);
    }

    private void OnMinimizeWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Host is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnToggleMaximizeWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Host is { } window)
        {
            ToggleMaximize(window);
        }
    }

    private void OnCloseWindowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Host?.Close();
    }

    private static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
