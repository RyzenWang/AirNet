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
            ("Settings", "04-settings"),
        };

        await Task.Delay(800);

        foreach (var (mode, suffix) in new[] { ("Light", "light"), ("Dark", "dark") })
        {
            viewModel.SetThemeModeCommand.Execute(mode);
            await Task.Delay(600);

            foreach (var (command, file) in pages)
            {
                viewModel.NavigateCommand.Execute(command);
                await Task.Delay(500);
                Capture(Path.Combine(directory, $"{file}-{suffix}.png"), scale);
            }
        }

        viewModel.NavigateCommand.Execute("Overview");
        viewModel.SetThemeModeCommand.Execute("Light");
        await Task.Delay(400);
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
