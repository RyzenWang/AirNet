using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AirNet.App.ViewModels;
using AirNet.App.Views;

namespace AirNet.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Headless measurement run, used to verify the probe stack without a window.
            if (ReadStringArgument(desktop.Args, "--selftest") is { } reportPath)
            {
                _ = RunSelfTestAsync(reportPath, desktop);
                return;
            }

            var shell = new ShellViewModel();
            var window = new MainWindow { DataContext = shell };
            desktop.MainWindow = window;

            var exportDirectory = ReadExportDirectory(desktop.Args);
            if (exportDirectory is { } directory)
            {
                // Off-screen capture mode: the window is real (so layout, resources and DPI are
                // authentic) but parked outside the visible desktop.
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(-4200, -4200);
                window.ShowInTaskbar = false;
                window.Show();
                var scale = ReadIntArgument(desktop.Args, "--scale") ?? 2;
                _ = window.ExportScreenshotsAsync(directory, scale);
            }
            else
            {
                window.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? ReadExportDirectory(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--export" or "-e")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }


        return null;
    }

    private static int? ReadIntArgument(string[]? args, string name)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadStringArgument(string[]? args, string name)
    {
        if (args is null)
        {
            return null;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Runs the real probe stack once and writes a flat report. Confirms that ICMP, DNS and the
    /// interface inventory work on the host without needing the UI.
    /// </summary>
    private static async Task RunSelfTestAsync(
        string reportPath,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var report = new System.Text.StringBuilder();
        try
        {
            var snapshot = await new Core.Probes.MeasurementRunner().RunAsync().ConfigureAwait(true);
            report.AppendLine($"captured={snapshot.CapturedAt:O}");
            report.AppendLine($"interface={snapshot.Interface.Name}");
            report.AppendLine($"kind={snapshot.Interface.Kind}");
            report.AppendLine($"local={snapshot.Interface.LocalAddress}");
            report.AppendLine($"gateway={snapshot.Interface.Gateway}");
            report.AppendLine($"dns={snapshot.Interface.DnsServer}");
            report.AppendLine($"avg={snapshot.Latency.AverageMs}");
            report.AppendLine($"p95={snapshot.Latency.P95Ms}");
            report.AppendLine($"jitter={snapshot.Latency.JitterMs}");
            report.AppendLine($"loss={snapshot.Latency.LossPercent}");
            report.AppendLine($"sent={snapshot.Latency.Sent}");
            report.AppendLine($"received={snapshot.Latency.Received}");
            report.AppendLine($"dnsLookupMs={snapshot.DnsMs}");
            report.AppendLine($"score={snapshot.Score}");
            report.AppendLine($"grade={snapshot.GradeLabel}");
            report.AppendLine($"verdict={snapshot.Verdict}");
            report.AppendLine($"hops={snapshot.Hops.Count}");
            report.AppendLine($"findings={snapshot.Findings.Count}");
            report.AppendLine($"samples={snapshot.LatencySeries.Count}");
        }
        catch (Exception ex)
        {
            report.AppendLine("error=" + ex);
        }

        await File.WriteAllTextAsync(reportPath, report.ToString()).ConfigureAwait(false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }
}
