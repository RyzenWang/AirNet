using AirNet.Core.Diagnostics;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class DualStackTests
{
    [Fact]
    public void Both_families_missing_is_unavailable()
    {
        var result = DualStackAnalyzer.Evaluate(null, null, 0, 0);

        Assert.Equal(DualStackVerdict.Unavailable, result.Verdict);
        Assert.False(result.HasV6);
    }

    [Fact]
    public void Missing_ipv6_is_reported_as_v4_only_not_as_a_failure()
    {
        var result = DualStackAnalyzer.Evaluate(21.4, null, 5, 0);

        Assert.Equal(DualStackVerdict.V4Only, result.Verdict);
        Assert.Equal("仅 IPv4", result.VerdictText);
        Assert.Contains("IPv6", result.Note);
    }

    [Fact]
    public void Ipv6_only_host_is_recognised()
    {
        var result = DualStackAnalyzer.Evaluate(null, 18.2, 0, 5);

        Assert.Equal(DualStackVerdict.V6Only, result.Verdict);
        Assert.Equal("不可用", new DualStackResult(DualStackVerdict.V4Only, null, null, 0, 0, "").V6Text);
    }

    [Theory]
    [InlineData(21.4, 25.9)]   // 1.21x — inside the noise band
    [InlineData(30.0, 31.0)]
    [InlineData(40.0, 40.0)]
    public void Comparable_timings_are_balanced(double v4, double v6)
    {
        var result = DualStackAnalyzer.Evaluate(v4, v6, 5, 5);

        Assert.Equal(DualStackVerdict.Balanced, result.Verdict);
        Assert.False(result.IsV6Slower);
    }

    [Fact]
    public void Clearly_faster_ipv6_is_preferred()
    {
        var result = DualStackAnalyzer.Evaluate(40.0, 20.0, 5, 5);

        Assert.Equal(DualStackVerdict.V6Preferred, result.Verdict);
        Assert.Contains("快", result.Note);
    }

    [Fact]
    public void Clearly_slower_ipv6_is_flagged()
    {
        var result = DualStackAnalyzer.Evaluate(20.0, 60.0, 5, 5);

        Assert.Equal(DualStackVerdict.V6Slower, result.Verdict);
        Assert.True(result.IsV6Slower);
        Assert.Contains("IPv4", result.Note);
    }

    [Fact]
    public void Thresholds_are_inclusive_at_the_boundary()
    {
        // Exactly at the faster threshold still counts as faster.
        Assert.Equal(
            DualStackVerdict.V6Preferred,
            DualStackAnalyzer.Evaluate(100, 100 * DualStackAnalyzer.FasterThreshold, 5, 5).Verdict);

        // Exactly at the slower threshold also counts as slower.
        Assert.Equal(
            DualStackVerdict.V6Slower,
            DualStackAnalyzer.Evaluate(100, 100 * DualStackAnalyzer.SlowerThreshold, 5, 5).Verdict);
    }

    [Fact]
    public void Text_never_renders_a_missing_family_as_zero()
    {
        var result = DualStackAnalyzer.Evaluate(21.4, null, 5, 0);

        Assert.Equal("21.4 ms", result.V4Text);
        Assert.Equal("不可用", result.V6Text);
    }
}
