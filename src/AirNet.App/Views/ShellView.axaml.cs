using Avalonia.Controls;
using Avalonia.Input;

namespace AirNet.App.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

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
