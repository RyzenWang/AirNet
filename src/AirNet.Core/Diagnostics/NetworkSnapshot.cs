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

/// <summary>One active network interface as the probe layer sees it.</summary>
public sealed record InterfaceInfo(
    string Name,
    string Kind,
    bool IsWireless,
    int? SignalPercent,
    double LinkSpeedMbps,
    string LocalAddress,
    string Gateway,
    string DnsServer)
{
    public static InterfaceInfo Unknown { get; } = new(
        "未检测到活动网卡", "未知", false, null, 0, "-", "-", "-");
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

/// <summary>A single hop on the path to the reference host.</summary>
public sealed record HopReading(
    string Label,
    string Address,
    double LatencyMs,
    bool Responded,
    bool IsTerminal = false);

/// <summary>How well a usage scenario is expected to behave on this path.</summary>
public sealed record ScenarioReading(string Name, int Score, string Hint)
{
    public string ScoreText => Score.ToString();
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
    double DnsMs,
    IReadOnlyList<double> LatencySeries,
    IReadOnlyList<HopReading> Hops,
    IReadOnlyList<ScenarioReading> Scenarios,
    IReadOnlyList<Finding> Findings,
    double DownloadMbps,
    double UploadMbps,
    DateTimeOffset CapturedAt,
    bool IsSample = false)
{
    public string ScoreText => Score.ToString();

    public string CapturedAtText => CapturedAt.ToLocalTime().ToString("HH:mm:ss");

    public string EgressText => string.IsNullOrWhiteSpace(EgressAddress) ? "未获取" : EgressAddress;
}

/// <summary>Raw probe output, before scoring.</summary>
public sealed record MeasurementResult(
    InterfaceInfo Interface,
    LatencyStats Latency,
    double DnsMs,
    IReadOnlyList<double> LatencySeries,
    IReadOnlyList<HopReading> Hops,
    string EgressAddress,
    DateTimeOffset CapturedAt);
