using AirNet.Core.History;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class HistoryDeltaTests
{
    [Fact]
    public void No_previous_run_means_no_delta()
    {
        var delta = HistoryDelta.Compare(Record(score: 90, latency: 20, down: 400), null);

        Assert.True(delta.IsEmpty);
        Assert.Equal(string.Empty, delta.Text);
    }

    [Fact]
    public void A_better_score_reads_as_an_improvement()
    {
        var delta = HistoryDelta.Compare(
            Record(score: 92, latency: 18, down: 420),
            Record(score: 85, latency: 22, down: 380));

        Assert.Contains("评分 +7", delta.Text);
        Assert.Contains("下行 +40", delta.Text);
        Assert.True(delta.IsImprovement);
        Assert.False(delta.IsRegression);
    }

    [Fact]
    public void Lower_latency_is_an_improvement_even_though_the_number_fell()
    {
        var delta = HistoryDelta.Compare(
            Record(score: 90, latency: 15, down: 400),
            Record(score: 90, latency: 30, down: 400));

        // 30 -> 15 ms: the number fell, so the sign is negative; colour says it is good.
        Assert.Contains("延迟 −15.0", delta.Text);
        Assert.True(delta.IsImprovement);
    }

    [Fact]
    public void Higher_latency_is_a_regression()
    {
        var delta = HistoryDelta.Compare(
            Record(score: 90, latency: 40, down: 400),
            Record(score: 90, latency: 20, down: 400));

        Assert.Contains("延迟 +20.0", delta.Text);
        Assert.True(delta.IsRegression);
        Assert.False(delta.IsImprovement);
    }

    [Fact]
    public void Identical_runs_produce_no_text()
    {
        var delta = HistoryDelta.Compare(
            Record(score: 90, latency: 20, down: 400),
            Record(score: 90, latency: 20, down: 400));

        Assert.True(delta.IsEmpty);
        Assert.Equal(string.Empty, delta.Text);
    }

    [Fact]
    public void Jitter_is_reported_alongside_latency()
    {
        var delta = HistoryDelta.Compare(
            Record(score: 90, latency: 20, down: 400, jitter: 2),
            Record(score: 90, latency: 20, down: 400, jitter: 6));

        Assert.Contains("抖动 −4.0", delta.Text);
    }

    private static HistoryRecord Record(
        int score,
        double latency,
        double down,
        double jitter = 1,
        double loss = 0) => new()
    {
        At = DateTimeOffset.Now,
        Score = score,
        LatencyAvg = latency,
        Jitter = jitter,
        Loss = loss,
        DownMbps = down,
        UpMbps = 20,
        Kind = "有线以太网",
        Ssid = string.Empty,
        LocalAddress = "192.168.1.2",
        Verdict = "测试",
    };
}
