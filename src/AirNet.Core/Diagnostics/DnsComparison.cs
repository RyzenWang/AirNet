namespace AirNet.Core.Diagnostics;

/// <summary>One resolver's answer time for the same question.</summary>
public sealed record DnsServerTiming(string Label, string Address, double? MedianMs, int Answered)
{
    public bool Responded => MedianMs is not null;

    public string TimeText => MedianMs is { } value ? $"{value:0.0} ms" : "未响应";
}

/// <summary>
/// The evidence behind the DNS recommendation: what the current resolver costs versus two
/// public ones, measured on the same host in the same run.
/// </summary>
public sealed record DnsComparison(
    DnsServerTiming? Current,
    IReadOnlyList<DnsServerTiming> Alternatives,
    DnsServerTiming? Best,
    bool WorthChanging,
    string Verdict,
    string Detail)
{
    public static DnsComparison Unavailable { get; } =
        new(null, [], null, false, "未测公共 DNS", "本次没有测到公共 DNS 的响应");

    public bool HasEvidence => Current is not null && Best is not null;
}

/// <summary>Pure comparison, so the recommendation thresholds can be tested.</summary>
public static class DnsComparisonAnalyzer
{
    /// <summary>An alternative must be at least this much faster in relative terms.</summary>
    public const double RelativeGain = 0.8;

    /// <summary>…and at least this many milliseconds faster, or the difference is noise.</summary>
    public const double AbsoluteGainMs = 5;

    public static DnsComparison Evaluate(
        string currentLabel,
        string currentAddress,
        double? currentMs,
        IReadOnlyList<DnsServerTiming> alternatives)
    {
        var answered = alternatives.Where(a => a.Responded).ToList();
        var current = currentMs is { } value && value > 0
            ? new DnsServerTiming(currentLabel, currentAddress, Math.Round(value, 1), 1)
            : null;

        if (current is null && answered.Count == 0)
        {
            return DnsComparison.Unavailable;
        }

        if (answered.Count == 0)
        {
            return new DnsComparison(
                current,
                alternatives,
                null,
                false,
                "公共 DNS 未响应",
                "两个公共 DNS 都没有在超时内回答，可能被当前网络拦截；结论仍按本次解析耗时给出。");
        }

        var best = answered.OrderBy(a => a.MedianMs).First();

        if (current?.MedianMs is not { } currentValue)
        {
            return new DnsComparison(
                current,
                alternatives,
                best,
                false,
                $"{best.Label} 最快（{best.TimeText}）",
                "本次没有测到本地解析耗时，无法判断更换是否划算。");
        }

        var bestValue = best.MedianMs!.Value;
        var faster = bestValue <= currentValue * RelativeGain;
        var meaningful = currentValue - bestValue >= AbsoluteGainMs;
        var worthChanging = faster && meaningful;

        var verdict = worthChanging
            ? $"换成 {best.Label} 可快 {currentValue - bestValue:0.0} ms"
            : bestValue < currentValue
                ? "公共 DNS 略快，但差距不值得改"
                : "当前 DNS 已经是最快的";

        var detail = worthChanging
            ? $"同一域名实测：当前 {currentValue:0.0} ms，{best.Label} {bestValue:0.0} ms。"
              + "DNS 慢会拖慢每个新连接的首包，切换后可用同一页复测验证。"
            : $"同一域名实测：当前 {currentValue:0.0} ms，最快公共 DNS {bestValue:0.0} ms，"
              + "差距在噪声范围内，改 DNS 不会有可感知收益。";

        return new DnsComparison(current, alternatives, best, worthChanging, verdict, detail);
    }
}
