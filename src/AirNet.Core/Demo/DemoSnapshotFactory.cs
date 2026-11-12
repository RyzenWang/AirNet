using AirNet.Core.Diagnostics;

namespace AirNet.Core.Demo;

/// <summary>
/// Deterministic sample measurement. The prototype opens on this so the visual language can
/// be reviewed without depending on the reviewer's actual network. Numbers are internally
/// consistent: the score, grade, scenarios and findings all come from the same samples.
/// </summary>
public static class DemoSnapshotFactory
{
    public const string ReferenceHost = "223.5.5.5";

    public static MeasurementResult CreateMeasurement()
    {
        var series = CreateSeries();
        var samples = series.Select(v => (double?)v).ToList();
        var stats = LatencyAnalyzer.Analyze(samples);

        var nic = new InterfaceInfo(
            Name: "Wi-Fi (Intel AX211)",
            Kind: "Wi-Fi 6",
            IsWireless: true,
            SignalPercent: 56,
            RssiDbm: -72,
            Ssid: "AIRNET-5G",
            LinkSpeedMbps: 1201,
            LocalAddress: "192.0.2.24",
            Gateway: "192.0.2.1",
            DnsServer: "192.0.2.1");

        IReadOnlyList<HopReading> hops =
        [
            new("家庭网关", "192.0.2.1", 1.8, true),
            new("骨干节点", "198.51.100.9", 9.6, true),
            new("参考主机", ReferenceHost, Math.Round(stats.MedianMs, 1), true, IsTerminal: true),
        ];

        var timing = new LayerTiming(
            Host: "www.baidu.com",
            DnsMs: 8.1,
            TcpMs: 12.4,
            TlsMs: 38.2,
            TtfbMs: 46.9,
            Succeeded: true);

        var diagnostics = new ProbeDiagnostics(
            IcmpAnswered: true,
            TcpAnswered: true,
            IcmpSent: 20,
            IcmpReceived: 20,
            TcpSent: 12,
            TcpReceived: 12);

        // A plausible dual-stack reading: present but slightly slower, which is the common
        // residential IPv6 situation and exercises the "IPv6 偏慢" branch in the UI.
        var dualStack = DualStackAnalyzer.Evaluate(v4Ms: 21.4, v6Ms: 25.9, v4Samples: 5, v6Samples: 5);

        return new MeasurementResult(
            nic,
            stats,
            ProbeMode.Icmp,
            diagnostics,
            timing,
            dualStack,
            // The sample deliberately shows no proxy/VPN so the default screenshots stay clean;
            // the branch is covered by unit tests.
            InterferenceInfo.None,
            DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.0.2.1", 8.1, DemoServers()),
            series,
            hops,
            EgressAddress: "203.0.113.42",
            CapturedAt: DateTimeOffset.Now);
    }

    public static NetworkSnapshot CreateSnapshot(double downloadMbps = 384.2, double uploadMbps = 92.6)
    {
        var snapshot = DiagnosisEngine.Evaluate(CreateMeasurement(), downloadMbps, uploadMbps);
        return snapshot with { IsSample = true };
    }

    /// <summary>Sixty samples shaped like a healthy 5 GHz link with two realistic spikes.</summary>
    public static IReadOnlyList<double> CreateSeries()
    {
        var random = new Random(20261111);
        var series = new List<double>(60);
        for (var i = 0; i < 60; i++)
        {
            var drift = Math.Sin(i / 7.2) * 1.7;
            var noise = (random.NextDouble() - 0.5) * 4.4;
            var value = 22.6 + drift + noise;

            if (i == 17)
            {
                value += 8.5;
            }

            if (i == 44)
            {
                value += 13.2;
            }

            series.Add(Math.Round(Math.Max(11, value), 1));
        }

        return series;
    }

    /// <summary>
    /// Sample resolver timings: both publics answer, and neither beats the current 8.1 ms — the
    /// demo should not recommend a change the sample data does not support.
    /// </summary>
    private static IReadOnlyList<DnsServerTiming> DemoServers() =>
    [
        new("阿里 DNS", "223.5.5.5", 12.4, 3),
        new("Cloudflare", "1.1.1.1", 28.6, 3),
    ];
}
