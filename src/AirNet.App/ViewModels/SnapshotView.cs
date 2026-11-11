using System.Globalization;
using AirNet.Core.Diagnostics;

namespace AirNet.App.ViewModels;

internal static class Fmt
{
    public static string Fixed(double value, int decimals = 1) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static string Compact(double value) =>
        value >= 100
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>One number on the dashboard, with the caption that explains it.</summary>
public sealed class MetricTile
{
    public required string Label { get; init; }
    public required string Value { get; init; }

    /// <summary>The raw number, so the tile can count up instead of snapping.</summary>
    public double Number { get; init; }

    public int Decimals { get; init; } = 1;
    public required string Unit { get; init; }
    public required string Foot { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}

public sealed class ScenarioRow
{
    public required string Name { get; init; }
    public required string Hint { get; init; }
    public required string ScoreText { get; init; }
    public required double Bar { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}

public sealed class HopRow
{
    public required string Label { get; init; }
    public required string Address { get; init; }
    public required string LatencyText { get; init; }
    public required double Bar { get; init; }
    public bool IsTerminal { get; init; }
}

public sealed class FindingRow
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public string? ActionLabel { get; init; }
    public string Glyph { get; init; } = "M20 6L9 17l-5-5";
    public bool HasAction => !string.IsNullOrEmpty(ActionLabel);
    public bool IsOk { get; init; }
    public bool IsInfo { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}

public sealed class RecentRun
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string ScoreText { get; init; }
    public required string TimeText { get; init; }
    public bool IsGood { get; init; }
    public bool IsWarn { get; init; }
    public bool IsBad { get; init; }
}

public sealed class SpeedHistoryBar
{
    public required string Label { get; init; }
    public required string ValueText { get; init; }
    public required double BarHeight { get; init; }
    public bool IsLatest { get; init; }
}

/// <summary>One segment of the first-byte bar; widths are in device-independent pixels.</summary>
public sealed class TimingSegment
{
    public required string Label { get; init; }
    public required double Width { get; init; }
    public required double Opacity { get; init; }
}

/// <summary>
/// Immutable projection of a snapshot into display strings. Keeping formatting here means the
/// XAML stays free of converters, which keeps the visual layer reviewable.
/// </summary>
public sealed class SnapshotView
{
    public SnapshotView(NetworkSnapshot snapshot)
    {
        Source = snapshot;
        Series = snapshot.LatencySeries;
        Metrics = BuildMetrics(snapshot);
        Scenarios = BuildScenarios(snapshot);
        Hops = BuildHops(snapshot);
        Findings = BuildFindings(snapshot);
    }

    public NetworkSnapshot Source { get; }

    public int Score => Source.Score;
    public string ScoreText => Source.Score.ToString(CultureInfo.InvariantCulture);
    public string GradeLabel => Source.GradeLabel;
    public string Verdict => Source.Verdict;
    public string Summary => Source.Summary;

    public IReadOnlyList<double> Series { get; }
    public IReadOnlyList<MetricTile> Metrics { get; }
    public IReadOnlyList<ScenarioRow> Scenarios { get; }
    public IReadOnlyList<HopRow> Hops { get; }
    public IReadOnlyList<FindingRow> Findings { get; }

    public double LatencyAvg => Source.Latency.AverageMs;
    public string LatencyAvgText => Fmt.Fixed(Source.Latency.AverageMs);
    public string LatencyP95Text => Fmt.Fixed(Source.Latency.P95Ms);
    public string JitterText => Fmt.Fixed(Source.Latency.JitterMs);
    public string LossText => Fmt.Fixed(Source.Latency.LossPercent);
    public string DownloadText => Fmt.Compact(Source.DownloadMbps);
    public string UploadText => Fmt.Compact(Source.UploadMbps);
    public string DnsText => Source.DnsMs <= 0 ? "未测" : Fmt.Fixed(Source.DnsMs);

    public string SampleCountText => $"{Source.Latency.Sent} 次采样";
    public string PeakText => Series.Count == 0 ? "-" : $"峰值 {Series.Max():0} ms";
    public string CapturedAtText => Source.CapturedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public string CapturedDateText => Source.CapturedAt.ToLocalTime().ToString("M月d日 HH:mm", CultureInfo.InvariantCulture);

    public string InterfaceChip => Source.Interface.Kind;
    public string SignalChip => Source.Interface.SignalPercent is { } signal
        ? Source.Interface.RssiDbm is { } dbm ? $"{signal}% · {dbm} dBm" : $"{signal}% 信号"
        : Source.Interface.IsWireless ? "无线" : "有线";
    public string EgressChip => $"出口 {Source.EgressText}";
    public string EgressText => Source.EgressText;
    public string LocalAddress => Source.Interface.LocalAddress;
    public string GatewayAddress => Source.Interface.Gateway;
    public string DnsServer => Source.Interface.DnsServer;
    public double SignalPercent => Source.Interface.SignalPercent ?? (Source.Interface.IsWireless ? 0 : 100);

    public string ProbeModeText => Source.ProbeModeText;
    public string SsidText => string.IsNullOrEmpty(Source.Interface.Ssid)
        ? Source.Interface.Kind
        : $"{Source.Interface.Kind} · {Source.Interface.Ssid}";

    // App-layer breakdown -------------------------------------------------
    public bool HasTiming => Source.Timing.Succeeded;
    public string TimingHostText => Source.Timing.Host;
    public string TimingTotalText => Source.Timing.Succeeded ? $"{Source.Timing.TotalMs:0} ms" : "未测";
    public string TimingLegendText => Source.Timing.Succeeded
        ? $"DNS {Source.Timing.DnsMs:0} · TCP {Source.Timing.TcpMs:0} · TLS {Source.Timing.TlsMs:0} · 等待 {Source.Timing.TtfbMs:0}"
        : Source.Timing.FailureNote ?? string.Empty;

    public IReadOnlyList<TimingSegment> TimingSegments => BuildTimingSegments(Source.Timing);

    public bool IsSample => Source.IsSample;

    public bool IsGood => Source.Grade is HealthGrade.Excellent;
    public bool IsWarn => Source.Grade is HealthGrade.Good or HealthGrade.Fair;
    public bool IsBad => Source.Grade is HealthGrade.Poor;

    private static IReadOnlyList<MetricTile> BuildMetrics(NetworkSnapshot s)
    {
        var latency = s.Latency.AverageMs;
        var jitter = s.Latency.JitterMs;
        var loss = s.Latency.LossPercent;
        var down = s.DownloadMbps;

        return
        [
            new MetricTile
            {
                Label = "延迟 / LATENCY",
                Value = Fmt.Fixed(latency),
                Number = latency,
                Decimals = 1,
                Unit = "ms",
                Foot = $"P95 {Fmt.Fixed(s.Latency.P95Ms)} ms",
                IsGood = latency <= 40,
                IsWarn = latency is > 40 and <= 90,
                IsBad = latency > 90,
            },
            new MetricTile
            {
                Label = "抖动 / JITTER",
                Value = Fmt.Fixed(jitter),
                Number = jitter,
                Decimals = 1,
                Unit = "ms",
                Foot = "相邻包差均值",
                IsGood = jitter <= 5,
                IsWarn = jitter is > 5 and <= 15,
                IsBad = jitter > 15,
            },
            new MetricTile
            {
                Label = "丢包 / LOSS",
                Value = Fmt.Fixed(loss),
                Number = loss,
                Decimals = 1,
                Unit = "%",
                Foot = $"{s.Latency.Received}/{s.Latency.Sent} 已返回",
                IsGood = loss <= 0.5,
                IsWarn = loss is > 0.5 and <= 3,
                IsBad = loss > 3,
            },
            new MetricTile
            {
                Label = "下载 / DOWNLOAD",
                Value = Fmt.Compact(down),
                Number = down,
                Decimals = down >= 100 ? 0 : 1,
                Unit = "Mbps",
                Foot = $"上行 {Fmt.Compact(s.UploadMbps)} Mbps",
                IsGood = down >= 100,
                IsWarn = down is >= 30 and < 100,
                IsBad = down < 30,
            },
        ];
    }

    private static IReadOnlyList<ScenarioRow> BuildScenarios(NetworkSnapshot s)
    {
        return s.Scenarios.Select(item => new ScenarioRow
        {
            Name = item.Name,
            Hint = item.Hint,
            ScoreText = item.ScoreText,
            Bar = Math.Clamp(item.Score / 100d, 0, 1),
            IsGood = item.Score >= 85,
            IsWarn = item.Score is >= 70 and < 85,
            IsBad = item.Score < 70,
        }).ToList();
    }

    private static IReadOnlyList<HopRow> BuildHops(NetworkSnapshot s)
    {
        var longest = s.Hops.Count == 0 ? 1 : Math.Max(1, s.Hops.Max(h => h.LatencyMs));
        return s.Hops.Select(hop => new HopRow
        {
            Label = hop.Label,
            Address = hop.Address,
            LatencyText = hop.Responded ? $"{Fmt.Fixed(hop.LatencyMs)} ms" : "超时",
            Bar = hop.Responded ? Math.Clamp(hop.LatencyMs / longest, 0.06, 1) : 0.06,
            IsTerminal = hop.IsTerminal,
        }).ToList();
    }

    /// <summary>
    /// Lays the four segments out proportionally across a fixed 284px track (the width of the
    /// link card's inner area). A minimum width keeps a fast segment visible.
    /// </summary>
    private static IReadOnlyList<TimingSegment> BuildTimingSegments(LayerTiming timing)
    {
        const double trackWidth = 284;
        if (!timing.Succeeded)
        {
            return [];
        }

        var total = Math.Max(timing.TotalMs, 1);
        (string Label, double Ms, double Opacity)[] parts =
        [
            ("DNS", timing.DnsMs, 1.0),
            ("TCP", timing.TcpMs, 0.8),
            ("TLS", timing.TlsMs, 0.6),
            ("等待", timing.TtfbMs, 0.42),
        ];

        return parts
            .Where(p => p.Ms > 0)
            .Select(p => new TimingSegment
            {
                Label = p.Label,
                Width = Math.Max(4, Math.Round(trackWidth * (p.Ms / total), 1)),
                Opacity = p.Opacity,
            })
            .ToList();
    }

    private static IReadOnlyList<FindingRow> BuildFindings(NetworkSnapshot s)
    {
        return s.Findings.Select(finding => new FindingRow
        {
            Title = finding.Title,
            Detail = finding.Detail,
            ActionLabel = finding.ActionLabel,
            Glyph = finding.Severity switch
            {
                Severity.Ok => "M20 6L9 17l-5-5",
                Severity.Info => "M12 16v-4M12 8h.01",
                Severity.Warn => "M12 9v4M12 17h.01",
                _ => "M12 9v4M12 17h.01",
            },
            IsOk = finding.Severity == Severity.Ok,
            IsInfo = finding.Severity == Severity.Info,
            IsWarn = finding.Severity == Severity.Warn,
            IsBad = finding.Severity == Severity.Bad,
        }).ToList();
    }
}
