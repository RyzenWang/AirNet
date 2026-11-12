using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AirNet.App.ViewModels;
using AirNet.App.Views;
using AirNet.Core.Sites;

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

                // Static captures: no counting numbers, no sweeping charts, no fading pages.
                Features.Motion.Enabled = false;
            }

            // Headless measurement run, used to verify the probe stack without a window.
            if (ReadStringArgument(desktop.Args, "--selftest") is { } reportPath)
            {
                _ = RunSelfTestAsync(reportPath, desktop);
                return;
            }

            // Headless website-latency run, used by CI and for a quick terminal check.
            if (ReadStringArgument(desktop.Args, "--sites") is { } sitesPath)
            {
                _ = RunSitesAsync(sitesPath, desktop);
                return;
            }

            // Headless report: one HTML file from a real scan, for CI and for sharing.
            if (ReadStringArgument(desktop.Args, "--report") is { } htmlPath)
            {
                _ = RunReportAsync(htmlPath, desktop);
                return;
            }

            // Headless bandwidth test, both directions, so upload can be verified without a UI.
            if (ReadStringArgument(desktop.Args, "--speedtest") is { } speedPath)
            {
                _ = RunSpeedTestAsync(speedPath, desktop);
                return;
            }

            // Scriptable probe: stable JSON plus an exit code that carries the verdict.
            if (desktop.Args.Contains("--probe"))
            {
                _ = RunProbeAsync(ReadStringArgument(desktop.Args, "--json"), desktop);
                return;
            }

            // Developer aid: capture the window while a real scan is in flight.
            var scanShotPath = ReadStringArgument(desktop.Args, "--scan-shot");

            var shell = new ShellViewModel();
            var window = new MainWindow { DataContext = shell };
            desktop.MainWindow = window;

            if (scanShotPath is not null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(-4200, -4200);
                window.ShowInTaskbar = false;
                window.Show();
                _ = window.CaptureScanningAsync(
                    scanShotPath,
                    ReadIntArgument(desktop.Args, "--scale") ?? 1,
                    ReadIntArgument(desktop.Args, "--shot-delay") ?? 1200);
                return;
            }

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
            report.AppendLine($"egress={snapshot.EgressText}");
            report.AppendLine($"hasBandwidth={snapshot.HasBandwidth}");
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
            report.AppendLine($"dualStack={snapshot.DualStack.Verdict} v4={snapshot.DualStack.V4Text} "
                              + $"v6={snapshot.DualStack.V6Text} note={snapshot.DualStack.Note}");
            report.AppendLine($"interference={snapshot.Interference.ChipText} proxy={snapshot.Interference.ProxyText} "
                              + $"tunnel={snapshot.Interference.TunnelText}");

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
            report.AppendLine($"traceOk={trace.Succeeded} hops={trace.Hops.Count} reached={trace.ReachedHop} targetReached={trace.ReachedTarget} spike={trace.SpikeIndex}");
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

            var dnsPlan = Core.Diagnostics.DnsAdvisor.Plan(snapshot.Interface, snapshot.Timing, snapshot.Dns);
            report.AppendLine($"dnsCurrent={snapshot.Dns.Current?.TimeText ?? "-"} "
                              + string.Join(" ", snapshot.Dns.Alternatives.Select(a => $"{a.Address}={a.TimeText}")));
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

    /// <summary>Runs the 11 default sites plus custom sites and writes a flat, greppable result.</summary>
    private static async Task RunSitesAsync(
        string outputPath,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var report = new System.Text.StringBuilder();
        try
        {
            var customSites = new CustomSiteStore().Load();
            var sites = SiteCatalog.WithCustom(customSites);
            using var overall = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await SiteLatencyProbe.RunAsync(
                sites,
                samplesPerSite: 3,
                concurrency: 4,
                timeoutMs: 4000,
                cancellationToken: overall.Token).ConfigureAwait(true);

            report.AppendLine($"captured={DateTimeOffset.Now:O}");
            foreach (var site in result.Results)
            {
                var median = site.Median;
                report.AppendLine(
                    $"{site.Target.Name}\t{site.Target.Host}\t{site.Success}\t{site.FailureKind}\t"
                    + $"{site.TotalMs:0.0}\t{(median?.DnsMs ?? 0):0.0}\t{(median?.TcpMs ?? 0):0.0}\t"
                    + $"{(median?.TlsMs ?? 0):0.0}\t{(median?.TtfbMs ?? 0):0.0}\t"
                    + $"{site.HttpStatus?.ToString() ?? "-"}\t{site.FailureNote}");
            }

            report.AppendLine($"succeeded={result.Succeeded}/{result.Results.Count} duration={result.Duration.TotalSeconds:0.0}s viaProxy={result.ViaProxy}");
            try
            {
                await new SiteHistoryStore().AppendAsync(result).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                report.AppendLine($"historyError={ex.Message}");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine("error=" + ex);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, report.ToString(), System.Text.Encoding.UTF8).ConfigureAwait(false);
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
            Core.Sites.SiteLatencyReport? siteHistory = null;
            try
            {
                siteHistory = await new SiteHistoryStore().LoadLatestAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A stale or locked sites.jsonl must not block the main report.
            }

            var html = Core.Reporting.ReportBuilder.Build(
                snapshot,
                attribution,
                mtu,
                siteLatency: siteHistory);
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

    /// <summary>Runs both bandwidth directions once and writes the numbers to a text file.</summary>
    private static async Task RunSpeedTestAsync(
        string path,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var report = new System.Text.StringBuilder();
        try
        {
            report.AppendLine($"captured={DateTimeOffset.Now:O}");
            var download = await Core.Probes.ThroughputProbe
                .MeasureDownloadMbpsAsync("https://speed.cloudflare.com/__down?bytes=26214400")
                .ConfigureAwait(true);
            report.AppendLine($"downloadMbps={download}");

            var upload = await Core.Probes.ThroughputProbe
                .MeasureUploadMbpsAsync("https://speed.cloudflare.com/__up", 4 * 1024 * 1024)
                .ConfigureAwait(true);
            report.AppendLine($"uploadMbps={upload}");
            report.AppendLine($"bothMeasured={(download > 0 && upload > 0)}");
        }
        catch (Exception ex)
        {
            report.AppendLine("error=" + ex);
        }

        await File.WriteAllTextAsync(path, report.ToString()).ConfigureAwait(false);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }

    /// <summary>
    /// One scan, one JSON document, and an exit code that says what happened:
    /// 0 healthy (score 75+), 1 degraded (60-74), 2 poor (&lt;60), 3 the scan itself failed.
    /// </summary>
    private static async Task RunProbeAsync(
        string? jsonPath,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var target = string.IsNullOrWhiteSpace(jsonPath) ? "airnet-probe.json" : jsonPath;
        var exitCode = 3;

        try
        {
            var snapshot = await new Core.Probes.MeasurementRunner().RunAsync().ConfigureAwait(true);
            var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var json = Core.Reporting.ProbeJsonContext.Serialize(Core.Reporting.ProbeJson.From(snapshot, version));

            var directory = Path.GetDirectoryName(Path.GetFullPath(target));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(target, json, System.Text.Encoding.UTF8).ConfigureAwait(false);

            exitCode = snapshot.Score switch
            {
                >= 75 => 0,
                >= 60 => 1,
                _ => 2,
            };
        }
        catch (Exception ex)
        {
            try
            {
                await File.WriteAllTextAsync(
                    target,
                    "{\"schemaVersion\":1,\"error\":\"" + ex.GetType().Name + "\"}",
                    System.Text.Encoding.UTF8).ConfigureAwait(false);
            }
            catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException)
            {
                // Nothing left to report to; the exit code still says the scan failed.
            }

            exitCode = 3;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown(exitCode));
    }
}
