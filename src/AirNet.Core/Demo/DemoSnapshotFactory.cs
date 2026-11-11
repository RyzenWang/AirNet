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
            Kind: "Wi-Fi 5 GHz",
            IsWireless: true,
            SignalPercent: 56,
            LinkSpeedMbps: 1201,
            LocalAddress: "192.0.2.24",
            Gateway: "192.168.1.1",
            DnsServer: "223.5.5.5");

        IReadOnlyList<HopReading> hops =
        [
            new("家庭网关", "192.168.1.1", 1.8, true),
            new("运营商接入", "10.32.0.1", 7.4, true),
            new("骨干节点", "198.51.100.9", 9.6, true),
            new("参考主机", ReferenceHost, Math.Round(stats.MedianMs, 1), true, IsTerminal: true),
        ];

        return new MeasurementResult(
            nic,
            stats,
            DnsMs: 8.1,
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
}
