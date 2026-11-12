using AirNet.Core.Diagnostics;

namespace AirNet.Core.Tests;

public sealed class LatencyAnalyzerTests
{
    [Fact]
    public void EmptySamplesAreEmpty()
    {
        var stats = LatencyAnalyzer.Analyze([]);

        Assert.Equal(0, stats.AverageMs);
        Assert.Equal(0, stats.Sent);
        Assert.Equal(0, stats.Received);
        Assert.Equal(100, stats.LossPercent);
    }

    [Fact]
    public void LostSamplesCountTowardLossButNotAverage()
    {
        var stats = LatencyAnalyzer.Analyze([10, null, 20]);

        Assert.Equal(3, stats.Sent);
        Assert.Equal(2, stats.Received);
        Assert.Equal(15, stats.AverageMs);
        Assert.Equal(33.3, stats.LossPercent);
    }

    [Fact]
    public void JitterIsMeanAbsoluteDifferenceOfAdjacentReplies()
    {
        var stats = LatencyAnalyzer.Analyze([10, 20, 30]);

        Assert.Equal(20, stats.AverageMs);
        Assert.Equal(10, stats.JitterMs);
        Assert.Equal(29, stats.P95Ms);
    }

    [Fact]
    public void AllLostSamplesKeepTheSentCount()
    {
        var stats = LatencyAnalyzer.Analyze([null, null, null]);

        Assert.Equal(3, stats.Sent);
        Assert.Equal(0, stats.Received);
        Assert.Equal(100, stats.LossPercent);
        Assert.Equal(0, stats.AverageMs);
    }
}
