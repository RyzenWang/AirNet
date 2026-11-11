namespace AirNet.Core.Diagnostics;

/// <summary>Pure statistics helpers over raw probe samples. Lost samples are null.</summary>
public static class LatencyAnalyzer
{
    public static LatencyStats Analyze(IReadOnlyList<double?> samples)
    {
        if (samples.Count == 0)
        {
            return LatencyStats.Empty;
        }

        var received = samples.Where(s => s.HasValue).Select(s => s!.Value).OrderBy(v => v).ToArray();
        var sent = samples.Count;
        if (received.Length == 0)
        {
            return LatencyStats.Empty with { Sent = sent, Received = 0 };
        }

        var average = received.Average();
        var median = Percentile(received, 0.5);
        var p95 = Percentile(received, 0.95);

        // Jitter is the mean absolute difference between consecutive replies, the metric
        // that actually predicts whether real-time traffic (calls, games) will stutter.
        var deltas = new List<double>();
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].HasValue && samples[i - 1].HasValue)
            {
                deltas.Add(Math.Abs(samples[i]!.Value - samples[i - 1]!.Value));
            }
        }

        var jitter = deltas.Count > 0 ? deltas.Average() : 0;
        var loss = (sent - received.Length) * 100.0 / sent;

        return new LatencyStats(
            Round(average),
            Round(median),
            Round(p95),
            Round(jitter),
            Round(loss),
            sent,
            received.Length);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var position = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = position - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * weight);
    }

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
