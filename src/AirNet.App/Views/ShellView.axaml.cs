using System.ComponentModel;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AirNet.App.Features;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class ShellView : UserControl
{
    private ShellViewModel? _shell;

    public ShellView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = DataContext as ShellViewModel;
        if (_shell is not null)
        {
            _shell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.PageIndex))
        {
            AnimateActivePage();
        }
    }

    /// <summary>
    /// Fades the incoming page instead of cutting to it. Only opacity is animated: a slide would
    /// fight the layout on the wider pages and makes a tool feel slower than it is.
    /// </summary>
    private void AnimateActivePage()
    {
        if (!Motion.Enabled || PageHost is null)
        {
            return;
        }

        var page = PageHost.Children.OfType<Control>().FirstOrDefault(c => c.IsVisible);
        if (page is null)
        {
            return;
        }

        page.Transitions = null;
        page.Opacity = 0;
        page.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = Motion.Page,
                Easing = new CubicEaseOut(),
            },
        };

        Dispatcher.UIThread.Post(() => page.Opacity = 1, DispatcherPriority.Render);
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
