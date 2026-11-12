namespace AirNet.Core.Diagnostics;

/// <summary>What the IPv4/IPv6 comparison concluded.</summary>
public enum DualStackVerdict
{
    /// <summary>Neither family answered; nothing to say.</summary>
    Unavailable,

    /// <summary>Host has no reachable IPv6 — the normal case on many networks.</summary>
    V4Only,

    /// <summary>IPv6 answered but IPv4 did not.</summary>
    V6Only,

    /// <summary>Both work and the difference is within noise.</summary>
    Balanced,

    /// <summary>IPv6 is measurably faster.</summary>
    V6Preferred,

    /// <summary>IPv6 works but is materially slower — usually a tunnel or a broken path.</summary>
    V6Slower,
}

/// <summary>
/// Side-by-side IPv4/IPv6 timing to the same host. Answered "is my IPv6 actually good", which a
/// single latency number cannot, because most stacks silently prefer whatever the resolver
/// returned first.
/// </summary>
public sealed record DualStackResult(
    DualStackVerdict Verdict,
    double? V4Ms,
    double? V6Ms,
    int V4Samples,
    int V6Samples,
    string Note)
{
    public static DualStackResult Unavailable { get; } =
        new(DualStackVerdict.Unavailable, null, null, 0, 0, "未测量");

    public bool HasV6 => V6Ms is not null;

    public string V4Text => V4Ms is { } v4 ? $"{v4:0.0} ms" : "—";

    public string V6Text => V6Ms is { } v6 ? $"{v6:0.0} ms" : "不可用";

    public string VerdictText => Verdict switch
    {
        DualStackVerdict.V4Only => "仅 IPv4",
        DualStackVerdict.V6Only => "仅 IPv6",
        DualStackVerdict.Balanced => "双栈均衡",
        DualStackVerdict.V6Preferred => "IPv6 更快",
        DualStackVerdict.V6Slower => "IPv6 偏慢",
        _ => "未测量",
    };

    /// <summary>True when IPv6 is present but slow enough that preferring it would hurt.</summary>
    public bool IsV6Slower => Verdict == DualStackVerdict.V6Slower;
}

/// <summary>Pure comparison logic, so the thresholds can be tested without a network.</summary>
public static class DualStackAnalyzer
{
    /// <summary>Below this ratio IPv6 counts as meaningfully faster.</summary>
    public const double FasterThreshold = 0.9;

    /// <summary>Above this ratio IPv6 counts as meaningfully slower.</summary>
    public const double SlowerThreshold = 1.3;

    public static DualStackResult Evaluate(double? v4Ms, double? v6Ms, int v4Samples, int v6Samples)
    {
        if (v4Ms is null && v6Ms is null)
        {
            return new DualStackResult(
                DualStackVerdict.Unavailable, null, null, v4Samples, v6Samples, "两种协议族都没有响应");
        }

        if (v6Ms is null)
        {
            return new DualStackResult(
                DualStackVerdict.V4Only, v4Ms, null, v4Samples, 0,
                "该主机未提供可用的 IPv6 地址，或本机没有 IPv6 出口");
        }

        if (v4Ms is null)
        {
            return new DualStackResult(
                DualStackVerdict.V6Only, null, v6Ms, 0, v6Samples, "仅 IPv6 可达");
        }

        var ratio = v6Ms.Value / Math.Max(v4Ms.Value, 0.1);
        var delta = Math.Abs(v6Ms.Value - v4Ms.Value);

        if (ratio <= FasterThreshold)
        {
            return new DualStackResult(
                DualStackVerdict.V6Preferred, v4Ms, v6Ms, v4Samples, v6Samples,
                $"IPv6 快 {delta:0.0} ms");
        }

        if (ratio >= SlowerThreshold)
        {
            return new DualStackResult(
                DualStackVerdict.V6Slower, v4Ms, v6Ms, v4Samples, v6Samples,
                $"IPv6 慢 {delta:0.0} ms，优先走 IPv4 更稳");
        }

        return new DualStackResult(
            DualStackVerdict.Balanced, v4Ms, v6Ms, v4Samples, v6Samples,
            $"两条路差 {delta:0.0} ms，属于噪声范围");
    }
}
