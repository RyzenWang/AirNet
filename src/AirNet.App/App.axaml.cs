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
            var exportDirectory = ReadExportDirectory(desktop.Args);

            // Screenshots must not depend on (or leak) the local measurement history.
            if (exportDirectory is not null)
            {
                Environment.SetEnvironmentVariable(
                    "AIRNET_DATA_DIR", Path.Combine(Path.GetTempPath(), "airnet-export-data"));
            }

            // Headless measurement run, used to verify the probe stack without a window.
            if (ReadStringArgument(desktop.Args, "--selftest") is { } reportPath)
            {
                _ = RunSelfTestAsync(reportPath, desktop);
                return;
            }

            // Headless report: one HTML file from a real scan, for CI and for sharing.
            if (ReadStringArgument(desktop.Args, "--report") is { } htmlPath)
            {
                _ = RunReportAsync(htmlPath, desktop);
                return;
            }

            var shell = new ShellViewModel();
            var window = new MainWindow { DataContext = shell };
            desktop.MainWindow = window;

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
            report.AppendLine($"ssid={snapshot.Interface.Ssid}");
            report.AppendLine($"signal={snapshot.Interface.SignalPercent}");
            report.AppendLine($"rssi={snapshot.Interface.RssiDbm}");
            report.AppendLine($"linkMbps={snapshot.Interface.LinkSpeedMbps}");
            report.AppendLine($"local={snapshot.Interface.LocalAddress}");
            report.AppendLine($"gateway={snapshot.Interface.Gateway}");
            report.AppendLine($"dns={snapshot.Interface.DnsServer}");
            report.AppendLine($"probeMode={snapshot.ProbeMode}");
            report.AppendLine($"avg={snapshot.Latency.AverageMs}");
            report.AppendLine($"p95={snapshot.Latency.P95Ms}");
            report.AppendLine($"jitter={snapshot.Latency.JitterMs}");
            report.AppendLine($"loss={snapshot.Latency.LossPercent}");
            report.AppendLine($"sent={snapshot.Latency.Sent}");
            report.AppendLine($"received={snapshot.Latency.Received}");
            report.AppendLine($"timingHost={snapshot.Timing.Host}");
            report.AppendLine($"dnsLookupMs={snapshot.Timing.DnsMs}");
            report.AppendLine($"tcpMs={snapshot.Timing.TcpMs}");
            report.AppendLine($"tlsMs={snapshot.Timing.TlsMs}");
            report.AppendLine($"ttfbMs={snapshot.Timing.TtfbMs}");
            report.AppendLine($"timingOk={snapshot.Timing.Succeeded}");
            report.AppendLine($"timingNote={snapshot.Timing.FailureNote}");
            report.AppendLine($"score={snapshot.Score}");
            report.AppendLine($"grade={snapshot.GradeLabel}");
            report.AppendLine($"verdict={snapshot.Verdict}");
            report.AppendLine($"hops={snapshot.Hops.Count}");
            report.AppendLine($"findings={snapshot.Findings.Count}");
            report.AppendLine($"samples={snapshot.LatencySeries.Count}");

            // The store must survive trimming: source-generated JSON is the only reason it does.
            var store = new Core.History.HistoryStore(
                Path.Combine(Path.GetTempPath(), "airnet-selftest-history"));
            await store.ClearAsync().ConfigureAwait(true);
            await store.AppendAsync(Core.Demo.DemoSnapshotFactory.CreateSnapshot()).ConfigureAwait(true);
            var reloaded = await store.LoadAsync(5).ConfigureAwait(true);
            report.AppendLine($"historyRoundTrip={(reloaded.Count == 1 && reloaded[0].Score > 0)}");
            report.AppendLine($"historyScore={reloaded.FirstOrDefault()?.Score}");
            await store.ClearAsync().ConfigureAwait(true);

            // Route discovery: the parser must survive a localized traceroute.
            var trace = await Core.Probes.TraceProbe.TraceAsync("223.5.5.5").ConfigureAwait(true);
            report.AppendLine($"traceOk={trace.Succeeded} hops={trace.Hops.Count} reached={trace.ReachedHop} spike={trace.SpikeIndex}");
            foreach (var hop in trace.Hops)
            {
                report.AppendLine($"  hop{hop.Index} {hop.Address} {(hop.Responded ? $"{hop.LatencyMs:0.0}ms" : "timeout")}");
            }

            report.AppendLine($"traceNote={trace.Note}");

            var mtu = await Core.Probes.MtuProbe.MeasureAsync("223.5.5.5").ConfigureAwait(true);
            report.AppendLine($"mtu={mtu.MtuBytes} payload={mtu.PayloadBytes} mtuOk={mtu.Succeeded} note={mtu.Note}");

            var attribution = Core.Diagnostics.AttributionEngine.FromTrace(snapshot, trace, mtu);
            report.AppendLine($"culprit={attribution.Culprit} precise={attribution.IsPrecise} headline={attribution.Headline}");

            // Report rendering and the DNS plan are deterministic; CI asserts on these lines.
            var html = Core.Reporting.ReportBuilder.Build(snapshot, attribution, mtu);
            report.AppendLine($"reportChars={html.Length}");
            report.AppendLine($"reportHasSvg={html.Contains("<svg", StringComparison.Ordinal)}");
            report.AppendLine($"reportHasScore={html.Contains("class=\"score\"", StringComparison.Ordinal)}");
            report.AppendLine($"reportMasked={Core.Reporting.ReportBuilder.Mask("192.168.1.42", true)}");

            var dnsPlan = Core.Diagnostics.DnsAdvisor.Plan(snapshot.Interface, snapshot.Timing);
            report.AppendLine($"dnsWorthChanging={dnsPlan.IsWorthChanging} dnsProposed={dnsPlan.ProposedDns}");
            report.AppendLine($"dnsApply={dnsPlan.ApplyCommand.Replace(Environment.NewLine, " && ")}");
            report.AppendLine($"dnsRestore={dnsPlan.RestoreCommand}");
        }
        catch (Exception ex)
        {
            report.AppendLine("error=" + ex);
        }

        await File.WriteAllTextAsync(reportPath, report.ToString()).ConfigureAwait(false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }

    /// <summary>One real scan plus route discovery, rendered straight to an HTML report.</summary>
    private static async Task RunReportAsync(
        string htmlPath,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var snapshot = await new Core.Probes.MeasurementRunner().RunAsync().ConfigureAwait(true);
            var trace = await Core.Probes.TraceProbe.TraceAsync("223.5.5.5").ConfigureAwait(true);
            var mtu = await Core.Probes.MtuProbe.MeasureAsync("223.5.5.5").ConfigureAwait(true);
            var attribution = Core.Diagnostics.AttributionEngine.FromTrace(snapshot, trace, mtu);

            var html = Core.Reporting.ReportBuilder.Build(snapshot, attribution, mtu);
            var directory = Path.GetDirectoryName(Path.GetFullPath(htmlPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(htmlPath, html, System.Text.Encoding.UTF8).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(htmlPath, "report failed: " + ex).ConfigureAwait(false);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }
}
