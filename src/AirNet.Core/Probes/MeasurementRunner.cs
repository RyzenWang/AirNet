using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>Which probe is running and how far along the scan is (0..1).</summary>
public sealed record ScanProgress(string Phase, double Fraction);

/// <summary>
/// Runs the unprivileged probe set end to end and hands the result to the diagnosis engine.
/// Cancellation is honoured between every probe, so the Stop button reacts immediately.
/// </summary>
public sealed class MeasurementRunner
{
    public MeasurementRunner(
        string referenceHost = "223.5.5.5",
        int referenceTcpPort = 53,
        string applicationHost = ApplicationTimingProbe.DefaultHost)
    {
        ReferenceHost = referenceHost;
        ReferenceTcpPort = referenceTcpPort;
        ApplicationHost = applicationHost;
    }

    /// <summary>Echo target, also used as the TCP timing target.</summary>
    public string ReferenceHost { get; }

    /// <summary>Port used for TCP handshake timing; 53 is answered by most public resolvers.</summary>
    public int ReferenceTcpPort { get; }

    /// <summary>Host used for the DNS/TCP/TLS/TTFB breakdown.</summary>
    public string ApplicationHost { get; }

    public async Task<NetworkSnapshot> RunAsync(
        double fallbackDownloadMbps = 0,
        double fallbackUploadMbps = 0,
        CancellationToken cancellationToken = default)
        => await RunAsync(fallbackDownloadMbps, fallbackUploadMbps, progress: null, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Same scan, but reports which probe is running so the UI can show progress instead of a
    /// frozen verdict. Reported from the probing thread; <c>Progress&lt;T&gt;</c> marshals it.
    /// </summary>
    public async Task<NetworkSnapshot> RunAsync(
        double fallbackDownloadMbps,
        double fallbackUploadMbps,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ScanProgress("识别网卡", 0.05));
        var nic = InterfaceInspector.Inspect();

        progress?.Report(new ScanProgress("ICMP 丢包探测", 0.12));
        var icmpSamples = await LatencyProbe.MeasureIcmpAsync(
            ReferenceHost, count: 20, intervalMs: 110, cancellationToken: cancellationToken).ConfigureAwait(false);
        var icmpStats = LatencyAnalyzer.Analyze(icmpSamples);

        progress?.Report(new ScanProgress("TCP 握手计时", 0.38));
        var tcpSamples = await LatencyProbe.MeasureTcpSeriesAsync(
            ReferenceHost,
            ReferenceTcpPort,
            count: 12,
            intervalMs: 80,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var tcpStats = LatencyAnalyzer.Analyze(tcpSamples);

        var diagnostics = new ProbeDiagnostics(
            IcmpAnswered: icmpStats.Received > 0,
            TcpAnswered: tcpStats.Received > 0,
            IcmpSent: icmpStats.Sent,
            IcmpReceived: icmpStats.Received,
            TcpSent: tcpStats.Sent,
            TcpReceived: tcpStats.Received);

        var (stats, series, mode) = SelectTiming(icmpStats, icmpSamples, tcpStats, tcpSamples);

        progress?.Report(new ScanProgress("首包四段分解", 0.68));
        var timing = await ApplicationTimingProbe.MeasureAsync(
            ApplicationHost, cancellationToken: cancellationToken).ConfigureAwait(false);

        progress?.Report(new ScanProgress("网关与链路", 0.86));
        var hops = new List<HopReading>();
        if (nic.Gateway is not "-" and not "")
        {
            var gatewaySamples = await LatencyProbe.MeasureIcmpAsync(
                nic.Gateway, count: 4, intervalMs: 60, cancellationToken: cancellationToken).ConfigureAwait(false);
            var gatewayStats = LatencyAnalyzer.Analyze(gatewaySamples);
            hops.Add(new HopReading("家庭网关", nic.Gateway, gatewayStats.AverageMs, gatewayStats.Received > 0));
        }

        hops.Add(new HopReading("参考主机", ReferenceHost, Math.Round(stats.MedianMs, 1), stats.Received > 0, true));

        progress?.Report(new ScanProgress("评分与归因", 0.96));

        var measurement = new MeasurementResult(
            nic, stats, mode, diagnostics, timing, series, hops, "未获取", DateTimeOffset.Now);

        var snapshot = DiagnosisEngine.Evaluate(measurement, fallbackDownloadMbps, fallbackUploadMbps);
        progress?.Report(new ScanProgress("完成", 1.0));
        return snapshot;
    }

    /// <summary>
    /// Picks the series the numbers are shown from. TCP handshakes are timed with a stopwatch and
    /// keep sub-millisecond resolution, so they win whenever they answer; ICMP still supplies the
    /// loss ratio, which TCP handshakes cannot observe.
    /// </summary>
    private static (LatencyStats Stats, IReadOnlyList<double> Series, ProbeMode Mode) SelectTiming(
        LatencyStats icmpStats,
        IReadOnlyList<double?> icmpSamples,
        LatencyStats tcpStats,
        IReadOnlyList<double?> tcpSamples)
    {
        if (tcpStats.Received >= 3)
        {
            var loss = icmpStats.Received > 0 ? icmpStats.LossPercent : tcpStats.LossPercent;
            var sent = icmpStats.Received > 0 ? icmpStats.Sent : tcpStats.Sent;
            var received = icmpStats.Received > 0 ? icmpStats.Received : tcpStats.Received;

            var merged = tcpStats with { LossPercent = loss, Sent = sent, Received = received };
            var series = tcpSamples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
            return (merged, series, ProbeMode.Tcp);
        }

        var icmpSeries = icmpSamples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        return (icmpStats, icmpSeries, ProbeMode.Icmp);
    }
}
