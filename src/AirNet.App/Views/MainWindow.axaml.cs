using Avalonia;
using Avalonia.Controls;
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
            ("Path", "04-path"),
            ("Settings", "05-settings"),
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
