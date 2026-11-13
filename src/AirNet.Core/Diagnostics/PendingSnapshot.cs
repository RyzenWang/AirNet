namespace AirNet.Core.Diagnostics;

/// <summary>
/// The honest first-run state: no score, no sample latency and no invented bandwidth. It is
/// replaced by live values as the probe phases land and by a real snapshot when the scan ends.
/// </summary>
public static class PendingSnapshot
{
    public static NetworkSnapshot Create() => new(
        0,
        HealthGrade.Poor,
        "等待检测",
        "等待检测",
        "点击「开始检测」后显示本次测量结果。",
        InterfaceInfo.Unknown,
        string.Empty,
        new LatencyStats(0, 0, 0, 0, 0, 0, 0),
        ProbeMode.Icmp,
        LayerTiming.None,
        DualStackResult.Unavailable,
        InterferenceInfo.None,
        DnsComparison.Unavailable,
        [],
        [],
        [],
        [],
        0,
        0,
        DateTimeOffset.Now,
        new ProbeDiagnostics(false, false, 0, 0, 0, 0),
        IsSample: false,
        IsPending: true);
}
