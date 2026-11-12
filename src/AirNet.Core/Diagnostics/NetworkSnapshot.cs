namespace AirNet.Core.Diagnostics;

/// <summary>Overall health of the measured path, from best to worst.</summary>
public enum HealthGrade
{
    Excellent,
    Good,
    Fair,
    Poor,
}

public enum Severity
{
    Ok,
    Info,
    Warn,
    Bad,
}

/// <summary>How the latency series was actually collected, so the UI can be honest about it.</summary>
public enum ProbeMode
{
    /// <summary>ICMP echo replies carried both latency and loss.</summary>
    Icmp,

    /// <summary>ICMP was filtered, so TCP handshakes carried the timing.</summary>
    Tcp,
}

/// <summary>One active network interface as the probe layer sees it.</summary>
public sealed record InterfaceInfo(
    string Name,
    string Kind,
    bool IsWireless,
    int? SignalPercent,
    int? RssiDbm,
    string Ssid,
    double LinkSpeedMbps,
    string LocalAddress,
    string Gateway,
    string DnsServer)
{
    public static InterfaceInfo Unknown { get; } = new(
        "未检测到活动网卡", "未知", false, null, null, string.Empty, 0, "-", "-", "-");

    public bool HasRadioDetail => IsWireless && SignalPercent is not null;

    public string RadioText => HasRadioDetail
        ? RssiDbm is { } rssi ? $"{SignalPercent}% · {rssi} dBm" : $"{SignalPercent}%"
        : IsWireless ? "无线" : "有线";
}

/// <summary>Aggregate latency statistics for one probe run.</summary>
public sealed record LatencyStats(
    double AverageMs,
    double MedianMs,
    double P95Ms,
    double JitterMs,
    double LossPercent,
    int Sent,
    int Received)
{
    public static LatencyStats Empty { get; } = new(0, 0, 0, 0, 100, 0, 0);
}

/// <summary>
/// The four segments a first request spends its time in. Measured on one connection, in order,
/// which is what makes "why is this page slow" answerable.
/// </summary>
public sealed record LayerTiming(
    string Host,
    double DnsMs,
    double TcpMs,
    double TlsMs,
    double TtfbMs,
    bool Succeeded,
    string? FailureNote = null,
    int? HttpStatus = null)
{
    public static LayerTiming None { get; } = new("-", 0, 0, 0, 0, false, "未测量");

    public double TotalMs => Math.Round(DnsMs + TcpMs + TlsMs + TtfbMs, 1);
}

/// <summary>A single hop on the path to the reference host.</summary>
public sealed record HopReading(
    string Label,
    string Address,
    double LatencyMs,
    bool Responded,
    bool IsTerminal = false);

/// <summary>How well a usage scenario is expected to behave on this path.</summary>
public sealed record ScenarioReading(string Name, int Score, string Hint, bool IsMeasured = true)
{
    public string ScoreText => IsMeasured ? Score.ToString() : "—";
}

/// <summary>One diagnosis line: what was measured, what it means, what to do.</summary>
public sealed record Finding(
    Severity Severity,
    string Title,
    string Detail,
    string? ActionLabel = null);

/// <summary>Everything the UI renders for one measurement.</summary>
public sealed record NetworkSnapshot(
    int Score,
    HealthGrade Grade,
    string GradeLabel,
    string Verdict,
    string Summary,
    InterfaceInfo Interface,
    string EgressAddress,
    LatencyStats Latency,
    ProbeMode ProbeMode,
    LayerTiming Timing,
    DualStackResult DualStack,
    InterferenceInfo Interference,
    IReadOnlyList<double> LatencySeries,
    IReadOnlyList<HopReading> Hops,
    IReadOnlyList<ScenarioReading> Scenarios,
    IReadOnlyList<Finding> Findings,
    double DownloadMbps,
    double UploadMbps,
    DateTimeOffset CapturedAt,
    ProbeDiagnostics Diagnostics,
    bool IsSample = false)
{
    public string ScoreText => Score.ToString();

    public string CapturedAtText => CapturedAt.ToLocalTime().ToString("HH:mm:ss");

    public string EgressText => string.IsNullOrWhiteSpace(EgressAddress) ? "未获取" : EgressAddress;

    /// <summary>Zero means the speed test has not run yet; it is not a measured 0 Mbps.</summary>
    public bool HasBandwidth => DownloadMbps > 0 || UploadMbps > 0;

    public bool HasUpload => UploadMbps > 0;

    public double DnsMs => Timing.DnsMs;

    public string ProbeModeText => ProbeMode == ProbeMode.Tcp ? "TCP 计时" : "ICMP 计时";
}

/// <summary>Which probes actually answered, so a silent ICMP can be explained rather than guessed.</summary>
public sealed record ProbeDiagnostics(
    bool IcmpAnswered,
    bool TcpAnswered,
    int IcmpSent,
    int IcmpReceived,
    int TcpSent,
    int TcpReceived);

/// <summary>Raw probe output, before scoring.</summary>
public sealed record MeasurementResult(
    InterfaceInfo Interface,
    LatencyStats Latency,
    ProbeMode ProbeMode,
    ProbeDiagnostics Diagnostics,
    LayerTiming Timing,
    DualStackResult DualStack,
    InterferenceInfo Interference,
    IReadOnlyList<double> LatencySeries,
    IReadOnlyList<HopReading> Hops,
    string EgressAddress,
    DateTimeOffset CapturedAt);
