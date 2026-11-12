using AirNet.Core.Diagnostics;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class DnsComparisonTests
{
    private static readonly DnsServerTiming Ali = new("阿里 DNS", "223.5.5.5", 12.0, 3);
    private static readonly DnsServerTiming Cloudflare = new("Cloudflare", "1.1.1.1", 30.0, 3);

    [Fact]
    public void A_much_faster_alternative_is_worth_switching_to()
    {
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 80, [Ali, Cloudflare]);

        Assert.True(comparison.WorthChanging);
        Assert.Equal("223.5.5.5", comparison.Best!.Address);
        Assert.Contains("阿里 DNS", comparison.Verdict);
        Assert.Contains("可快", comparison.Verdict);
    }

    [Fact]
    public void A_marginal_gain_is_not_worth_changing()
    {
        // 22 -> 18 ms: 18% faster but only 4 ms, under the absolute threshold.
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 22, [Ali with { MedianMs = 18 }]);

        Assert.False(comparison.WorthChanging);
        Assert.Contains("不值得", comparison.Verdict);
    }

    [Fact]
    public void A_relative_gain_that_is_too_small_is_rejected()
    {
        // 100 -> 85 ms: 15 ms better but only 15% — inside the noise band by ratio.
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 100, [Ali with { MedianMs = 85 }]);

        Assert.False(comparison.WorthChanging);
    }

    [Fact]
    public void A_faster_current_resolver_is_reported_as_already_best()
    {
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 8, [Ali, Cloudflare]);

        Assert.False(comparison.WorthChanging);
        Assert.Contains("已经是最快", comparison.Verdict);
    }

    [Fact]
    public void Silent_public_resolvers_are_not_reported_as_a_recommendation()
    {
        var silent = new[]
        {
            new DnsServerTiming("阿里 DNS", "223.5.5.5", null, 0),
            new DnsServerTiming("Cloudflare", "1.1.1.1", null, 0),
        };

        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 40, silent);

        Assert.False(comparison.WorthChanging);
        Assert.Equal("公共 DNS 未响应", comparison.Verdict);
        Assert.Null(comparison.Best);
    }

    [Fact]
    public void No_measurements_at_all_yields_unavailable()
    {
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", null, []);

        Assert.False(comparison.HasEvidence);
        Assert.Equal("未测公共 DNS", comparison.Verdict);
    }

    [Fact]
    public void A_missing_current_timing_still_shows_the_fastest_alternative()
    {
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", null, [Ali, Cloudflare]);

        Assert.Equal("223.5.5.5", comparison.Best!.Address);
        Assert.False(comparison.WorthChanging);
        Assert.Contains("无法判断", comparison.Detail);
    }

    [Fact]
    public void Unanswered_servers_are_excluded_from_the_best_pick()
    {
        var silent = new DnsServerTiming("阿里 DNS", "223.5.5.5", null, 0);
        var comparison = DnsComparisonAnalyzer.Evaluate("当前 DNS", "192.168.1.1", 60, [silent, Cloudflare]);

        Assert.Equal("1.1.1.1", comparison.Best!.Address);
        Assert.True(comparison.WorthChanging);
    }
}
