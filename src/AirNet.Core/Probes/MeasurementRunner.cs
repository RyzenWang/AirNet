using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Runs the unprivileged probe set end to end and hands the result to the diagnosis engine.
/// Cancellation is honoured between every probe, so the Stop button reacts immediately.
/// </summary>
public sealed class MeasurementRunner
{
    public MeasurementRunner(string referenceHost = "223.5.5.5")
    {
        ReferenceHost = referenceHost;
    }

    public string ReferenceHost { get; }

    public async Task<NetworkSnapshot> RunAsync(
        double fallbackDownloadMbps = 0,
        double fallbackUploadMbps = 0,
        CancellationToken cancellationToken = default)
    {
        var nic = InterfaceInspector.Inspect();

        var samples = await LatencyProbe.MeasureIcmpAsync(
            ReferenceHost, count: 20, intervalMs: 110, cancellationToken: cancellationToken).ConfigureAwait(false);

        // An all-lost ICMP series usually means echo is filtered, not that the link is down.
        if (samples.All(s => s is null))
        {
            var tcpSamples = new List<double?>();
            for (var i = 0; i < 12; i++)
            {
                tcpSamples.Add(await LatencyProbe.MeasureTcpAsync(
                    ReferenceHost, 53, cancellationToken: cancellationToken).ConfigureAwait(false));
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
            }

            samples = tcpSamples;
        }

        var stats = LatencyAnalyzer.Analyze(samples);
        var dnsMs = await LatencyProbe.MeasureDnsAsync("www.alidns.com", cancellationToken).ConfigureAwait(false);

        var hops = new List<HopReading>();
        if (nic.Gateway is not "-" and not "")
        {
            var gatewaySamples = await LatencyProbe.MeasureIcmpAsync(
                nic.Gateway, count: 4, intervalMs: 60, cancellationToken: cancellationToken).ConfigureAwait(false);
            var gatewayStats = LatencyAnalyzer.Analyze(gatewaySamples);
            hops.Add(new HopReading("家庭网关", nic.Gateway, gatewayStats.AverageMs, gatewayStats.Received > 0));
        }

        hops.Add(new HopReading("参考主机", ReferenceHost, Math.Round(stats.MedianMs, 1), stats.Received > 0, true));

        var series = samples.Where(s => s.HasValue).Select(s => s!.Value).ToList();

        var measurement = new MeasurementResult(
            nic, stats, dnsMs, series, hops, "未获取", DateTimeOffset.Now);

        return DiagnosisEngine.Evaluate(measurement, fallbackDownloadMbps, fallbackUploadMbps);
    }
}
