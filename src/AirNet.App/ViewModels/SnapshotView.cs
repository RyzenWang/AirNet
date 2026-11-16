using System.Globalization;
using AirNet.App.Localization;
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

    /// <summary>Bandwidth with its unit, or "未测" when no speed test has run.</summary>
    public static string Mbps(double value) => value > 0 ? $"{Compact(value)} Mbps" : LocalizationService.Current.T("L.NotMeasured");
}

/// <summary>One number on the dashboard, with the caption that explains it.</summary>
public sealed class MetricTile
{
    public required string Label { get; init; }
    public required string Value { get; init; }

    /// <summary>The raw number, so the tile can count up instead of snapping.</summary>
    public double Number { get; init; }

    public int Decimals { get; init; } = 1;

    /// <summary>True when there is no number yet; the tile shows <see cref="Value"/> instead.</summary>
    public bool IsPlaceholder { get; init; }

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
    public bool IsPlaceholder { get; init; }
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
    public string? ActionTarget { get; init; }
    public string Glyph { get; init; } = "M20 6L9 17l-5-5";
    public bool HasAction => !string.IsNullOrEmpty(ActionLabel) && ActionTarget is not null;
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

    /// <summary>Difference from the previous run; empty on the oldest row or when nothing moved.</summary>
    public string DeltaText { get; init; } = string.Empty;
    public bool HasDelta => !string.IsNullOrEmpty(DeltaText);
    public bool DeltaImproved { get; init; }
    public bool DeltaRegressed { get; init; }
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
        Metrics = snapshot.IsPending ? DemoTiles() : BuildMetrics(snapshot);
        Scenarios = BuildScenarios(snapshot);
        Hops = BuildHops(snapshot);
        Findings = BuildFindings(snapshot);
    }

    /// <summary>Placeholder tiles before the first measurement lands.</summary>
    private static string T(string key) => LocalizationService.Current.T(key);

    private string BuildEnglishSummary()
    {
        var link = Source.Interface.IsWireless
            ? Source.Interface.SignalPercent is { } signal
                ? LocalizationService.Current.Format("L.WifiSignalFormat", signal)
                : T("L.Wireless")
            : T("L.Wired");
        return LocalizationService.Current.Format(
            "L.SummaryFormat",
            $"{Source.Latency.AverageMs:0.0}",
            $"{Source.Latency.JitterMs:0.0}",
            $"{Source.Latency.LossPercent:0.0}",
            link);
    }

    private static string LocalizeScenarioName(string name) => name switch
    {
        "网页浏览" => T("L.WebBrowsing"),
        "4K 视频" => T("L.Video4K"),
        "在线游戏" => T("L.OnlineGaming"),
        "视频会议" => T("L.VideoMeeting"),
        "大文件下载" => T("L.LargeDownload"),
        _ => name,
    };

    private static string LocalizeScenarioHint(string hint) => hint switch
    {
        "首屏加载与翻页" => "First paint and page turns",
        "拖拽与缓冲速度" => "Scrubbing and buffering",
        "操作延迟与命中" => "Input latency and hit registration",
        "语音连续性与画质" => "Voice continuity and quality",
        "上行未测，仅按延迟估算" => "Upload not measured; estimated from latency",
        "峰值吞吐能力" => "Peak throughput",
        "未测速" => "Not tested",
        _ => hint,
    };

    public static IReadOnlyList<MetricTile> DemoTiles() =>
    [
        new MetricTile { Label = T("L.MetricLatency"), Value = "—", Unit = "ms", Foot = T("L.WaitingMeasurement"), IsPlaceholder = true, IsGood = true },
        new MetricTile { Label = T("L.MetricJitter"), Value = "—", Unit = "ms", Foot = T("L.WaitingMeasurement"), IsPlaceholder = true, IsGood = true },
        new MetricTile { Label = T("L.MetricLoss"), Value = "—", Unit = "%", Foot = T("L.WaitingMeasurement"), IsPlaceholder = true, IsGood = true },
        new MetricTile { Label = T("L.MetricDownload"), Value = "—", Unit = "Mbps", Foot = T("L.NotTested"), IsPlaceholder = true, IsGood = true },
    ];

    public NetworkSnapshot Source { get; }

    public int Score => Source.Score;
    public string ScoreText => IsPending ? "—" : Source.Score.ToString(CultureInfo.InvariantCulture);
    public string GradeLabel => IsPending ? T("L.WaitingForScan") : Source.Grade switch
    {
        HealthGrade.Excellent => T("L.Excellent"),
        HealthGrade.Good => T("L.Good"),
        HealthGrade.Fair => T("L.Fair"),
        _ => T("L.Poor"),
    };
    public string Verdict => IsPending ? T("L.WaitingForScan") : Source.Grade switch
    {
        HealthGrade.Excellent => T("L.VerdictExcellent"),
        HealthGrade.Good => T("L.VerdictGood"),
        HealthGrade.Fair => T("L.VerdictFair"),
        _ => T("L.VerdictPoor"),
    };
    public string Summary => IsPending
        ? Source.Summary
        : LocalizationService.Current.IsEnglish ? BuildEnglishSummary() : Source.Summary;
    public bool IsPending => Source.IsPending;

    public IReadOnlyList<double> Series { get; }
    public IReadOnlyList<MetricTile> Metrics { get; }
    public IReadOnlyList<ScenarioRow> Scenarios { get; }
    public IReadOnlyList<HopRow> Hops { get; }
    public IReadOnlyList<FindingRow> Findings { get; }

    public double LatencyAvg => Source.Latency.AverageMs;
    public string LatencyAvgText => IsPending ? "—" : Fmt.Fixed(Source.Latency.AverageMs);
    public string LatencyAvgCaption => LocalizationService.Current.Format("L.AverageWithFormat", LatencyAvgText);
    public string LatencyP95Caption => LocalizationService.Current.Format("L.P95Format", LatencyP95Text);
    public string LatencyP95Text => IsPending ? "—" : Fmt.Fixed(Source.Latency.P95Ms);
    public string JitterText => IsPending ? "—" : Fmt.Fixed(Source.Latency.JitterMs);
    public string LossText => IsPending ? "—" : Fmt.Fixed(Source.Latency.LossPercent);
    public string DownloadText => Source.DownloadMbps > 0 ? Fmt.Mbps(Source.DownloadMbps) : T("L.NotTested");
    public string UploadText => Fmt.Mbps(Source.UploadMbps);
    public bool HasBandwidth => Source.HasBandwidth;
    public string DnsText => Source.DnsMs <= 0 ? T("L.NotMeasured") : Fmt.Fixed(Source.DnsMs);

    public string SampleCountText => IsPending
        ? "—"
        : LocalizationService.Current.Format("L.SamplesFormat", Source.Latency.Sent);
    public string PeakText => Series.Count == 0
        ? "-"
        : LocalizationService.Current.Format("L.PeakFormat", $"{Series.Max():0}");
    public string CapturedAtText => IsPending ? "—" : Source.CapturedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public string CapturedDateText => IsPending
        ? T("L.NotScanned")
        : Source.CapturedAt.ToLocalTime().ToString(
            LocalizationService.Current.IsChinese ? "M月d日 HH:mm" : "MMM d HH:mm",
            LocalizationService.Current.Culture);

    public string InterfaceChip => IsPending ? T("L.NotDetected") : LocalizeInterfaceKind(Source.Interface.Kind);
    public string SignalChip => IsPending
        ? T("L.NotDetected")
        : Source.Interface.SignalPercent is { } signal
            ? Source.Interface.RssiDbm is { } dbm ? $"{signal}% · {dbm} dBm" : $"{signal}% {T("L.Signal")}"
            : Source.Interface.IsWireless ? T("L.Wireless") : T("L.Wired");
    public string EgressChip => LocalizationService.Current.Format("L.EgressFormat", EgressText);
    public string EgressText => Source.EgressText == "未获取" ? T("L.NotAcquired") : Source.EgressText;
    public string LocalAddress => Source.Interface.LocalAddress;
    public string GatewayAddress => Source.Interface.Gateway;
    public string DnsServer => Source.Interface.DnsServer;
    public double SignalPercent => Source.Interface.SignalPercent ?? (Source.Interface.IsWireless ? 0 : 100);

    public string ProbeModeText => IsPending
        ? T("L.ProbeWaiting")
        : Source.ProbeMode == ProbeMode.Tcp ? T("L.ProbeTcp") : T("L.ProbeIcmp");
    public string SsidText => string.IsNullOrEmpty(Source.Interface.Ssid)
        ? LocalizeInterfaceKind(Source.Interface.Kind)
        : $"{LocalizeInterfaceKind(Source.Interface.Kind)} · {Source.Interface.Ssid}";

    private static string LocalizeInterfaceKind(string kind) => kind switch
    {
        "有线以太网" => T("L.Ethernet"),
        "无线" => T("L.WirelessGeneric"),
        "未知" => T("L.Unknown"),
        _ => kind,
    };

    private static string LocalizeHopLabel(string label) => label switch
    {
        "家庭网关" => T("L.HopHome"),
        "参考主机" => T("L.HopReference"),
        _ => label,
    };

    // Dual stack ----------------------------------------------------------
    public string DualStackVerdictText => LocalizationService.Current.IsChinese
        ? Source.DualStack.VerdictText
        : Source.DualStack.Verdict switch
        {
            DualStackVerdict.V4Only => T("L.DualV4Only"),
            DualStackVerdict.V6Only => T("L.DualV6Only"),
            DualStackVerdict.Balanced => T("L.DualBalanced"),
            DualStackVerdict.V6Preferred => T("L.DualV6Faster"),
            DualStackVerdict.V6Slower => T("L.DualV6Slower"),
            _ => T("L.DualUnavailable"),
        };
    public string DualStackDetailText => Source.DualStack.Verdict == DualStackVerdict.Unavailable
        ? T("L.DualUnavailable")
        : $"IPv4 {Source.DualStack.V4Text} · IPv6 {(Source.DualStack.V6Ms is null ? T("L.NotAvailable") : Source.DualStack.V6Text)}";
    public string DualStackNote => LocalizationService.Current.IsChinese ? Source.DualStack.Note : T("L.DualNote");
    public bool IsV6Slower => Source.DualStack.IsV6Slower;

    // Proxy / VPN ---------------------------------------------------------
    public bool IsInterfering => Source.Interference.IsInterfering;
    public string InterferenceChip => Source.Interference.ChipText;
    public string InterferenceNote => Source.Interference.Note;

    // App-layer breakdown -------------------------------------------------
    public bool HasTiming => Source.Timing.Succeeded;
    public string TimingHostText => Source.Timing.Host;
    public string TimingTotalText => Source.Timing.Succeeded ? $"{Source.Timing.TotalMs:0} ms" : T("L.NotMeasured");
    public string TimingLegendText => Source.Timing.Succeeded
        ? $"DNS {Source.Timing.DnsMs:0} · TCP {Source.Timing.TcpMs:0} · TLS {Source.Timing.TlsMs:0} · 等待 {Source.Timing.TtfbMs:0}"
        : Source.Timing.FailureNote ?? string.Empty;

    public IReadOnlyList<TimingSegment> TimingSegments => BuildTimingSegments(Source.Timing);

    public bool IsSample => Source.IsSample;

    public bool IsGood => !IsPending && Source.Grade is HealthGrade.Excellent;
    public bool IsWarn => !IsPending && Source.Grade is HealthGrade.Good or HealthGrade.Fair;
    public bool IsBad => !IsPending && Source.Grade is HealthGrade.Poor;

    private static IReadOnlyList<MetricTile> BuildMetrics(NetworkSnapshot s)
    {
        var latency = s.Latency.AverageMs;
        var jitter = s.Latency.JitterMs;
        var loss = s.Latency.LossPercent;
        var down = s.DownloadMbps;
        var hasBandwidth = s.HasBandwidth;

        return
        [
            new MetricTile
            {
                Label = T("L.MetricLatency"),
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
                Label = T("L.MetricJitter"),
                Value = Fmt.Fixed(jitter),
                Number = jitter,
                Decimals = 1,
                Unit = "ms",
                Foot = T("L.AdjacentMean"),
                IsGood = jitter <= 5,
                IsWarn = jitter is > 5 and <= 15,
                IsBad = jitter > 15,
            },
            new MetricTile
            {
                Label = T("L.MetricLoss"),
                Value = Fmt.Fixed(loss),
                Number = loss,
                Decimals = 1,
                Unit = "%",
                Foot = $"{s.Latency.Received}/{s.Latency.Sent} {T("L.Returned")}",
                IsGood = loss <= 0.5,
                IsWarn = loss is > 0.5 and <= 3,
                IsBad = loss > 3,
            },
            new MetricTile
            {
                Label = T("L.MetricDownload"),
                Value = hasBandwidth ? Fmt.Compact(down) : "—",
                Number = down,
                Decimals = down >= 100 ? 0 : 1,
                Unit = "Mbps",
                Foot = s.HasUpload ? $"{T("L.Uplink")} {Fmt.Compact(s.UploadMbps)} Mbps" : T("L.NotTested"),
                IsPlaceholder = !hasBandwidth,
                IsGood = hasBandwidth && down >= 100,
                IsWarn = hasBandwidth && down is >= 30 and < 100,
                IsBad = hasBandwidth && down < 30,
            },
        ];
    }

    private static IReadOnlyList<ScenarioRow> BuildScenarios(NetworkSnapshot s)
    {
        return s.Scenarios.Select(item => new ScenarioRow
        {
            Name = LocalizeScenarioName(item.Name),
            Hint = LocalizationService.Current.IsChinese ? item.Hint : LocalizeScenarioHint(item.Hint),
            ScoreText = item.ScoreText,
            Bar = item.IsMeasured ? Math.Clamp(item.Score / 100d, 0, 1) : 0,
            IsPlaceholder = !item.IsMeasured,
            IsGood = item.IsMeasured && item.Score >= 85,
            IsWarn = item.IsMeasured && item.Score is >= 70 and < 85,
            IsBad = item.IsMeasured && item.Score < 70,
        }).ToList();
    }

    private static IReadOnlyList<HopRow> BuildHops(NetworkSnapshot s)
    {
        var longest = s.Hops.Count == 0 ? 1 : Math.Max(1, s.Hops.Max(h => h.LatencyMs));
        return s.Hops.Select(hop => new HopRow
        {
            Label = LocalizeHopLabel(hop.Label),
            Address = hop.Address,
            LatencyText = hop.Responded
                ? hop.LatencyMs < 1 ? "<1 ms" : $"{Fmt.Fixed(hop.LatencyMs)} ms"
                : "超时",
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
            (T("L.WaitSegment"), timing.TtfbMs, 0.42),
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

    private static string LocalizeFindingTitle(string title)
    {
        if (LocalizationService.Current.IsChinese)
        {
            return title;
        }

        if (title.StartsWith("丢包 ", StringComparison.Ordinal)) return title.Replace("丢包 ", "Packet loss ", StringComparison.Ordinal);
        if (title.StartsWith("抖动 ", StringComparison.Ordinal)) return title.Replace("抖动 ", "Jitter ", StringComparison.Ordinal).Replace(" 偏高", " high", StringComparison.Ordinal);
        if (title.StartsWith("DNS 解析 ", StringComparison.Ordinal)) return title.Replace("DNS 解析 ", "DNS lookup ", StringComparison.Ordinal);
        if (title.StartsWith("首包 ", StringComparison.Ordinal)) return title.Replace("首包 ", "First byte ", StringComparison.Ordinal);
        if (title.StartsWith("上行只有 ", StringComparison.Ordinal)) return title.Replace("上行只有 ", "Upload only ", StringComparison.Ordinal);
        return title switch
        {
            "无线信号偏弱" => "Weak Wi-Fi signal",
            "ICMP 被拦截" => "ICMP filtered",
            "首包分解不可用" => "First-byte split unavailable",
            "测量经过 VPN" => "Measurement goes through a VPN",
            "测量经过系统代理" => "Measurement goes through a system proxy",
            "IPv6 明显更慢" => "IPv6 is much slower",
            "IPv6 更快" => "IPv6 is faster",
            _ => title,
        };
    }

    private static string LocalizeFindingDetail(string title) => title switch
    {
        "Weak Wi-Fi signal" => "Signal is weak; move closer to the router or use 6 GHz.",
        "ICMP filtered" => "ICMP is filtered; TCP handshake timing is used instead and remains reliable.",
        "First-byte split unavailable" => "The first-byte timing could not be measured this time.",
        "Measurement goes through a VPN" => "The numbers describe the VPN exit, not the local carrier path.",
        "Measurement goes through a system proxy" => "HTTP measurements use the proxy; ICMP loss and TCP timing still use the real path.",
        "IPv6 is much slower" => "Applications may try IPv6 first; a fallback timeout becomes first-byte latency.",
        "IPv6 is faster" => "IPv6 is faster on this path; prefer it when the application supports it.",
        _ when title.StartsWith("First byte", StringComparison.Ordinal) => "The server or path is queueing after the TLS handshake.",
        _ when title.StartsWith("DNS lookup", StringComparison.Ordinal) => "Slow DNS delays every new connection; a lower-latency resolver helps.",
        _ when title.StartsWith("Packet loss", StringComparison.Ordinal) => "Loss above 2% breaks real-time video and calls. Check wireless interference or the cable.",
        _ when title.StartsWith("Jitter", StringComparison.Ordinal) => "Adjacent-packet delay varies; real-time voice and games will stutter.",
        _ when title.StartsWith("Upload only", StringComparison.Ordinal) => "The uplink is the bottleneck for meetings, backups and cloud sync.",
        _ => "See the measured values above.",
    };

    private static string? LocalizeAction(string? action) => action switch
    {
        "排查链路" => "Investigate path",
        "优化 DNS" => "Optimize DNS",
        "优化信号" => "Improve signal",
        "查看建议" => "View advice",
        _ => action,
    };

    private static IReadOnlyList<FindingRow> BuildFindings(NetworkSnapshot s)
    {
        return s.Findings.Select(finding =>
        {
            var title = LocalizeFindingTitle(finding.Title);
            return new FindingRow
        {
            Title = title,
            Detail = LocalizationService.Current.IsChinese ? finding.Detail : LocalizeFindingDetail(title),
            ActionLabel = LocalizationService.Current.IsChinese ? finding.ActionLabel : LocalizeAction(finding.ActionLabel),
            ActionTarget = finding.ActionLabel switch
            {
                "排查链路" => "Path",
                "优化 DNS" or "优化信号" or "查看建议" => "Settings",
                _ => null,
            },
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
            };
        }).ToList();
    }
}
