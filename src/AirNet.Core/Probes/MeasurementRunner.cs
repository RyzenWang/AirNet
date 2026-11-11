using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

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
    {
        var nic = InterfaceInspector.Inspect();

        var icmpSamples = await LatencyProbe.MeasureIcmpAsync(
            ReferenceHost, count: 20, intervalMs: 110, cancellationToken: cancellationToken).ConfigureAwait(false);
        var icmpStats = LatencyAnalyzer.Analyze(icmpSamples);

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

        var timing = await ApplicationTimingProbe.MeasureAsync(
            ApplicationHost, cancellationToken: cancellationToken).ConfigureAwait(false);

        var hops = new List<HopReading>();
        if (nic.Gateway is not "-" and not "")
        {
            var gatewaySamples = await LatencyProbe.MeasureIcmpAsync(
                nic.Gateway, count: 4, intervalMs: 60, cancellationToken: cancellationToken).ConfigureAwait(false);
            var gatewayStats = LatencyAnalyzer.Analyze(gatewaySamples);
            hops.Add(new HopReading("家庭网关", nic.Gateway, gatewayStats.AverageMs, gatewayStats.Received > 0));
        }

        hops.Add(new HopReading("参考主机", ReferenceHost, Math.Round(stats.MedianMs, 1), stats.Received > 0, true));

        var measurement = new MeasurementResult(
            nic, stats, mode, diagnostics, timing, series, hops, "未获取", DateTimeOffset.Now);

        return DiagnosisEngine.Evaluate(measurement, fallbackDownloadMbps, fallbackUploadMbps);
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
