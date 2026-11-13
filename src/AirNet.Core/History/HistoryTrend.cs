namespace AirNet.Core.History;

/// <summary>One point on the history chart, ordered oldest to newest by the analyzer.</summary>
public sealed record TrendPoint(
    DateTimeOffset At,
    int Score,
    double LatencyMs,
    double JitterMs,
    double LossPercent,
    double DownMbps);

/// <summary>Average for a six-hour local-time window, used to expose evening congestion.</summary>
public sealed record TimeOfDayBucket(
    string Label,
    string RangeText,
    int Count,
    double AverageLatencyMs,
    double AverageScore,
    double AverageLossPercent)
{
    public bool HasData => Count > 0;
}

public sealed record HistoryTrendSummary(
    int Count,
    IReadOnlyList<TrendPoint> Points,
    IReadOnlyList<TimeOfDayBucket> Buckets,
    double AverageScore,
    double AverageLatencyMs,
    double AverageJitterMs,
    double AverageLossPercent,
    double AverageDownMbps,
    TimeOfDayBucket? WorstBucket)
{
    public bool HasData => Count > 0;
}

/// <summary>
/// Turns persisted runs into chart-ready data. Filtering by days is done here rather than in the
/// UI so the same behavior is unit-tested and the 30-day retention stays the only storage limit.
/// </summary>
public static class HistoryTrendAnalyzer
{
    public static HistoryTrendSummary Analyze(
        IReadOnlyList<HistoryRecord> records,
        int days,
        DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.Now;
        var cutoff = reference.AddDays(-Math.Max(1, days));

        var points = records
            .Where(record => !record.IsSample && record.At >= cutoff && record.At <= reference)
            .OrderBy(record => record.At)
            .Select(record => new TrendPoint(
                record.At,
                record.Score,
                Math.Round(record.LatencyAvg, 1),
                Math.Round(record.Jitter, 1),
                Math.Round(record.Loss, 1),
                Math.Round(record.DownMbps, 1)))
            .ToList();

        if (points.Count == 0)
        {
            return new HistoryTrendSummary(0, [], EmptyBuckets(), 0, 0, 0, 0, 0, null);
        }

        var buckets = BuildBuckets(points);
        var measuredDown = points.Where(point => point.DownMbps > 0).ToList();
        var worst = buckets
            .Where(bucket => bucket.HasData)
            .OrderByDescending(bucket => bucket.AverageLatencyMs)
            .FirstOrDefault();

        return new HistoryTrendSummary(
            points.Count,
            points,
            buckets,
            Math.Round(points.Average(point => point.Score), 1),
            Math.Round(points.Average(point => point.LatencyMs), 1),
            Math.Round(points.Average(point => point.JitterMs), 1),
            Math.Round(points.Average(point => point.LossPercent), 1),
            measuredDown.Count == 0 ? 0 : Math.Round(measuredDown.Average(point => point.DownMbps), 1),
            worst);
    }

    private static IReadOnlyList<TimeOfDayBucket> BuildBuckets(IReadOnlyList<TrendPoint> points)
    {
        var definitions = new (string Label, string Range, int Start)[]
        {
            ("凌晨", "00–06", 0),
            ("上午", "06–12", 6),
            ("下午", "12–18", 12),
            ("晚间", "18–24", 18),
        };

        var result = new List<TimeOfDayBucket>(definitions.Length);
        foreach (var definition in definitions)
        {
            var bucketPoints = points
                .Where(point => point.At.ToLocalTime().Hour >= definition.Start
                                && point.At.ToLocalTime().Hour < definition.Start + 6)
                .ToList();
            result.Add(bucketPoints.Count == 0
                ? new TimeOfDayBucket(definition.Label, definition.Range, 0, 0, 0, 0)
                : new TimeOfDayBucket(
                    definition.Label,
                    definition.Range,
                    bucketPoints.Count,
                    Math.Round(bucketPoints.Average(point => point.LatencyMs), 1),
                    Math.Round(bucketPoints.Average(point => point.Score), 1),
                    Math.Round(bucketPoints.Average(point => point.LossPercent), 1)));
        }

        return result;
    }

    private static IReadOnlyList<TimeOfDayBucket> EmptyBuckets() =>
    [
        new("凌晨", "00–06", 0, 0, 0, 0),
        new("上午", "06–12", 0, 0, 0, 0),
        new("下午", "12–18", 0, 0, 0, 0),
        new("晚间", "18–24", 0, 0, 0, 0),
    ];
}
