using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                Shell.SyncWindowState(WindowState);
            }
        };

        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Window-level shortcuts. Input controls win first, so typing in a text field never starts a
    /// scan; everything else is deliberately global, matching the shortcuts card in Settings.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel shell || e.Source is TextBox)
        {
            return;
        }

        var commandKey = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                         || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (commandKey)
        {
            var page = e.Key switch
            {
                Key.D1 or Key.NumPad1 => "Overview",
                Key.D2 or Key.NumPad2 => "Monitor",
                Key.D3 or Key.NumPad3 => "Speed",
                Key.D4 or Key.NumPad4 => "Sites",
                Key.D5 or Key.NumPad5 => "Path",
                Key.D6 or Key.NumPad6 => "Settings",
                _ => null,
            };

            if (page is not null)
            {
                shell.NavigateCommand.Execute(page);
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Key.R:
                    shell.RunScanCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.T:
                    shell.ToggleThemeCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.Escape && shell.IsScanning)
        {
            shell.StopScanCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Renders every page in light and dark to PNG. Used while reviewing the visual language:
    /// the window is parked off-screen, so the exports match what the app actually draws.
    /// </summary>
    public async Task ExportScreenshotsAsync(string directory, int scale = 2)
    {
        Directory.CreateDirectory(directory);
        if (DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        var pages = new (string Command, string File)[]
        {
            ("Overview", "01-overview"),
            ("Monitor", "02-monitor"),
            ("Speed", "03-speed"),
            ("Sites", "04-sites"),
            ("Path", "05-path"),
            ("Settings", "06-settings"),
        };

        await Task.Delay(800);

        foreach (var (mode, suffix) in new[] { ("Light", "light"), ("Dark", "dark") })
        {
            viewModel.SetThemeModeCommand.Execute(mode);
            await Task.Delay(600);

            foreach (var (command, file) in pages)
            {
                viewModel.NavigateCommand.Execute(command);
                await Task.Delay(400);

                // The route page is empty until a trace runs; capture it with real data.
                if (command == "Path" && viewModel.Path.RunTraceCommand.CanExecute(null))
                {
                    await viewModel.Path.RunTraceCommand.ExecuteAsync(null);
                    await Task.Delay(400);
                }

                Capture(Path.Combine(directory, $"{file}-{suffix}.png"), scale);
            }
        }

        viewModel.NavigateCommand.Execute("Overview");
        viewModel.SetThemeModeCommand.Execute("Light");
        await Task.Delay(400);
        Close();
    }

    /// <summary>
    /// Captures the window mid-scan. The scanning state only exists for a couple of seconds, so
    /// this is the only way to review it (and to smoke-test the progress strip).
    /// </summary>
    public async Task CaptureScanningAsync(string path, int scale = 1, int delayMs = 1200)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        viewModel.NavigateCommand.Execute("Monitor");
        await Task.Delay(400);

        var scan = viewModel.RunScanCommand.ExecuteAsync(null);
        await Task.Delay(delayMs);
        Capture(path, scale);

        await scan;
        await Task.Delay(300);
        Close();
    }

    private void Capture(string path, int scale)
    {
        var width = (int)Math.Round(Bounds.Width);
        var height = (int)Math.Round(Bounds.Height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(width * scale, height * scale),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(Shell);
        bitmap.Save(path);
    }
}
