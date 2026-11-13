using AirNet.Core.History;

namespace AirNet.Core.Tests;

public sealed class HistoryTrendTests
{
    private static readonly DateTimeOffset Now = new(
        DateTime.Today.AddHours(20),
        TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddHours(20)));

    [Fact]
    public void FiltersRangeIgnoresSamplesAndOrdersOldestFirst()
    {
        var records = new[]
        {
            Record(Now.AddHours(-1), score: 90, latency: 20),
            Record(Now.AddDays(-3), score: 80, latency: 30),
            Record(Now.AddDays(-9), score: 70, latency: 40),
            Record(Now.AddHours(-2), score: 99, latency: 5, isSample: true),
        };

        var result = HistoryTrendAnalyzer.Analyze(records, days: 7, Now);

        Assert.Equal(2, result.Count);
        Assert.True(result.HasData);
        Assert.Equal(80, result.Points[0].Score);
        Assert.Equal(90, result.Points[1].Score);
        Assert.Equal(25, result.AverageLatencyMs);
        Assert.Equal(85, result.AverageScore);
    }

    [Fact]
    public void BuildsLocalTimeBucketsAndFindsWorstBucket()
    {
        var records = new[]
        {
            Record(AtLocalHour(2), score: 95, latency: 12),   // 凌晨 local
            Record(AtLocalHour(9), score: 90, latency: 18),   // 上午
            Record(AtLocalHour(14), score: 70, latency: 55),  // 下午
            Record(AtLocalHour(20), score: 80, latency: 42),  // 晚间
        };

        var result = HistoryTrendAnalyzer.Analyze(records, days: 7, Now);

        Assert.Equal(4, result.Buckets.Count);
        Assert.Equal(1, result.Buckets[0].Count);
        Assert.Equal(55, result.Buckets[2].AverageLatencyMs);
        Assert.Equal("下午", result.WorstBucket?.Label);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void EmptyRangeHasNoDataAndZeroAverages()
    {
        var result = HistoryTrendAnalyzer.Analyze([], days: 7, Now);

        Assert.False(result.HasData);
        Assert.Empty(result.Points);
        Assert.Empty(result.Points);
        Assert.Equal(0, result.AverageScore);
        Assert.All(result.Buckets, bucket => Assert.False(bucket.HasData));
        Assert.Null(result.WorstBucket);
    }

    [Fact]
    public void DownlinkAveragesOnlyMeasuredValues()
    {
        var records = new[]
        {
            Record(Now.AddHours(-1), score: 90, latency: 20, down: 100),
            Record(Now.AddHours(-2), score: 90, latency: 20, down: 0),
            Record(Now.AddHours(-3), score: 90, latency: 20, down: 300),
        };

        var result = HistoryTrendAnalyzer.Analyze(records, days: 7, Now);

        Assert.Equal(200, result.AverageDownMbps);
    }

    private static DateTimeOffset AtLocalHour(int hour)
    {
        var local = Now.Date.AddHours(hour);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static HistoryRecord Record(
        DateTimeOffset at,
        int score,
        double latency,
        bool isSample = false,
        double down = 0) => new()
    {
        At = at,
        Score = score,
        LatencyAvg = latency,
        Jitter = 2,
        Loss = 0,
        DownMbps = down,
        IsSample = isSample,
    };
}
