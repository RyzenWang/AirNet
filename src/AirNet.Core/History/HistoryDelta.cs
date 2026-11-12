namespace AirNet.Core.History;

/// <summary>
/// How this run compares with the previous one. The point is answering "did replacing the
/// router actually help" with numbers instead of a feeling.
/// </summary>
public sealed record HistoryDelta(
    double ScoreDelta,
    double LatencyDelta,
    double JitterDelta,
    double LossDelta,
    double DownDelta)
{
    public static HistoryDelta None { get; } = new(0, 0, 0, 0, 0);

    public bool IsEmpty => ScoreDelta == 0 && LatencyDelta == 0 && JitterDelta == 0
                           && LossDelta == 0 && DownDelta == 0;

    /// <summary>Compares against <paramref name="previous"/>; null yields no deltas.</summary>
    public static HistoryDelta Compare(HistoryRecord current, HistoryRecord? previous)
    {
        if (previous is null)
        {
            return None;
        }

        return new HistoryDelta(
            (double)(current.Score - previous.Score),
            Math.Round(current.LatencyAvg - previous.LatencyAvg, 1),
            Math.Round(current.Jitter - previous.Jitter, 1),
            Math.Round(current.Loss - previous.Loss, 1),
            Math.Round(current.DownMbps - previous.DownMbps, 1));
    }

    /// <summary>
    /// Compact line for the history row. Latency and jitter are inverted: fewer milliseconds is
    /// better, so their arrows must not read as "improved" when the number grew.
    /// </summary>
    public string Text
    {
        get
        {
            var parts = new List<string>(4);

            if (ScoreDelta != 0)
            {
                parts.Add($"评分 {Signed(ScoreDelta, "0")}");
            }

            if (LatencyDelta != 0)
            {
                parts.Add($"延迟 {Signed(LatencyDelta, "0.0")}");
            }

            if (JitterDelta != 0)
            {
                parts.Add($"抖动 {Signed(JitterDelta, "0.0")}");
            }

            if (DownDelta != 0)
            {
                parts.Add($"下行 {Signed(DownDelta, "0.#")}");
            }

            return parts.Count == 0 ? string.Empty : $"较上次 {string.Join(" · ", parts)}";
        }
    }

    /// <summary>True when the composite is an improvement, for tinting the row.</summary>
    public bool IsImprovement =>
        ScoreDelta > 0 || (ScoreDelta == 0 && LatencyDelta < 0);

    public bool IsRegression =>
        ScoreDelta < 0 || (ScoreDelta == 0 && LatencyDelta > 0);

    private static string Signed(double value, string format) =>
        value > 0
            ? $"+{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}"
            : $"−{Math.Abs(value).ToString(format, System.Globalization.CultureInfo.InvariantCulture)}";
}
