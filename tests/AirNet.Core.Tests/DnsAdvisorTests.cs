using AirNet.Core.Diagnostics;

namespace AirNet.Core.Tests;

public sealed class DnsAdvisorTests
{
    [Fact]
    public void DoesNotRecommendChangingAnAlreadyOptimalResolver()
    {
        var plan = DnsAdvisor.Plan(Interface("223.5.5.5"), Timing(dnsMs: 120));

        Assert.False(plan.IsWorthChanging);
        Assert.Contains("最优", plan.Headline);
    }

    [Fact]
    public void RecommendsAChangeWhenResolutionIsSlow()
    {
        var plan = DnsAdvisor.Plan(Interface("192.168.1.1"), Timing(dnsMs: 40));

        Assert.True(plan.IsWorthChanging);
        Assert.Contains("223.5.5.5", plan.ProposedDns);
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("source=dhcp", plan.RestoreCommand);
        }
    }

    [Fact]
    public void DoesNotRecommendAChangeForFastResolution()
    {
        var plan = DnsAdvisor.Plan(Interface("192.168.1.1"), Timing(dnsMs: 5));

        Assert.False(plan.IsWorthChanging);
    }

    [Fact]
    public void UnavailableTimingProducesNoPlan()
    {
        var plan = DnsAdvisor.Plan(Interface("192.168.1.1"), LayerTiming.None);

        Assert.False(plan.IsWorthChanging);
        Assert.Equal("-", plan.AdapterName);
    }

    private static InterfaceInfo Interface(string dns) => new(
        "Ethernet", "有线以太网", false, null, null, string.Empty,
        1000, "192.168.1.2", "192.168.1.1", dns);

    private static LayerTiming Timing(double dnsMs) =>
        new("www.baidu.com", dnsMs, 5, 20, 30, Succeeded: true);
}
