using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>Which probe is running and how far along the scan is (0..1).</summary>
public sealed record ScanProgress(string Phase, double Fraction);

/// <summary>
/// One probe result as it lands, with the running statistics over the samples so far. The UI
/// shows these directly, which is why the numbers during a scan are real measurements rather
/// than a decorative animation.
/// </summary>
public sealed record LiveSample(
    string Source,
    double? LatencyMs,
    double AverageMs,
    double JitterMs,
    double LossPercent,
    int Count);

/// <summary>
/// Runs the unprivileged probe set end to end and hands the result to the diagnosis engine.
/// Cancellation is honoured between every probe, so the Stop button reacts immediately.
/// </summary>
public sealed class MeasurementRunner
{
    private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=26214400";
    private const string UploadUrl = "https://speed.cloudflare.com/__up";

    /// <summary>
    /// 15 echo requests at 90ms keeps the ICMP phase near 1.4s; the whole scan stays under the
    /// three seconds the product promises, while still giving P95 something to stand on.
    /// </summary>
    public const int IcmpSampleCount = 15;

    /// <summary>Handshakes are slower than echoes, so fewer are needed for the same confidence.</summary>
    public const int TcpSampleCount = 10;

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
        => await RunAsync(
                fallbackDownloadMbps,
                fallbackUploadMbps,
                progress: null,
                live: null,
                dualProbe: true,
                includeBandwidth: false,
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Same scan, but reports which probe is running (and each sample as it lands) so the UI can
    /// show live progress instead of a frozen verdict.
    /// </summary>
    public async Task<NetworkSnapshot> RunAsync(
        double fallbackDownloadMbps,
        double fallbackUploadMbps,
        IProgress<ScanProgress>? progress,
        IProgress<LiveSample>? live = null,
        bool dualProbe = true,
        bool includeBandwidth = false,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new ScanProgress("识别网卡", 0.05));
        var nic = InterfaceInspector.Inspect();
        var interference = InterferenceProbe.Detect();

        var icmpLive = new List<double?>(IcmpSampleCount);
        var tcpLive = new List<double?>(TcpSampleCount);

        progress?.Report(new ScanProgress("ICMP 丢包探测", 0.12));
        var icmpSamples = await LatencyProbe.MeasureIcmpAsync(
            ReferenceHost,
            count: IcmpSampleCount,
            intervalMs: 90,
            cancellationToken: cancellationToken,
            onSample: sample => ReportLive(live, "ICMP", icmpLive, sample)).ConfigureAwait(false);
        var icmpStats = LatencyAnalyzer.Analyze(icmpSamples);

        IReadOnlyList<double?> tcpSamples;
        if (dualProbe)
        {
            progress?.Report(new ScanProgress("TCP 握手计时", 0.42));
            tcpSamples = await LatencyProbe.MeasureTcpSeriesAsync(
                ReferenceHost,
                ReferenceTcpPort,
                count: TcpSampleCount,
                intervalMs: 70,
                cancellationToken: cancellationToken,
                onSample: sample => ReportLive(live, "TCP", tcpLive, sample)).ConfigureAwait(false);
        }
        else
        {
            progress?.Report(new ScanProgress("TCP 计时已关闭", 0.42));
            tcpSamples = [];
        }

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

        // The egress lookup runs beside the first-byte breakdown; when Cloudflare is blocked the
        // scan still completes and the chip reads "未获取" instead of inventing an address.
        // Dual-stack timing runs beside them too, so the comparison costs no extra wall clock.
        var egressTask = EgressProbe.TryGetAsync(cancellationToken);
        var timingTask = ApplicationTimingProbe.MeasureAsync(
            ApplicationHost,
            proxy: HttpClient.DefaultProxy,
            cancellationToken: cancellationToken);
        var stackTask = DualStackProbe.MeasureAsync(ApplicationHost, cancellationToken: cancellationToken);
        var publicDnsTask = DnsProbe.MeasurePublicAsync(ApplicationHost, cancellationToken);

        await Task.WhenAll(timingTask, stackTask, publicDnsTask).ConfigureAwait(false);
        var timing = await timingTask.ConfigureAwait(false);
        var dualStack = await stackTask.ConfigureAwait(false);
        var dns = DnsComparisonAnalyzer.Evaluate(
            "当前 DNS", nic.DnsServer, (await timingTask.ConfigureAwait(false)).DnsMs,
            await publicDnsTask.ConfigureAwait(false));
        var egress = await egressTask.ConfigureAwait(false) ?? "未获取";

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

        var downloadMbps = fallbackDownloadMbps;
        var uploadMbps = fallbackUploadMbps;
        if (includeBandwidth)
        {
            progress?.Report(new ScanProgress("带宽测速", 0.97));

            // These run after all latency/TTFB probes: saturating the link first would make
            // the latency numbers describe a busy line rather than the user's normal one.
            var measuredDownload = await ThroughputProbe.MeasureDownloadMbpsAsync(
                DownloadUrl, maxDurationMs: 4000, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (measuredDownload > 0)
            {
                downloadMbps = measuredDownload;
            }

            var measuredUpload = await ThroughputProbe.MeasureUploadMbpsAsync(
                UploadUrl, bytes: 4 * 1024 * 1024, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (measuredUpload > 0)
            {
                uploadMbps = measuredUpload;
            }
        }

        progress?.Report(new ScanProgress("评分与归因", 0.99));

        var measurement = new MeasurementResult(
            nic, stats, mode, diagnostics, timing, dualStack, interference, dns, series, hops, egress, DateTimeOffset.Now);

        var snapshot = DiagnosisEngine.Evaluate(measurement, downloadMbps, uploadMbps);
        progress?.Report(new ScanProgress("完成", 1.0));
        return snapshot;
    }

    private static void ReportLive(
        IProgress<LiveSample>? live,
        string source,
        List<double?> collector,
        double? sample)
    {
        if (live is null)
        {
            return;
        }

        collector.Add(sample);
        var stats = LatencyAnalyzer.Analyze(collector);
        live.Report(new LiveSample(source, sample, stats.AverageMs, stats.JitterMs, stats.LossPercent, collector.Count));
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
