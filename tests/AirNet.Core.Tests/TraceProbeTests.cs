using AirNet.Core.Probes;

namespace AirNet.Core.Tests;

public sealed class TraceProbeTests
{
    [Fact]
    public void ParsesEnglishTracerouteOutputWithoutRelyingOnTimeoutText()
    {
        const string output = """
            Tracing route to 223.5.5.5 over a maximum of 16 hops

              1    <1 ms    <1 ms    <1 ms  192.168.1.1
              2     *        *        *     Request timed out.
              3     8 ms     9 ms     8 ms  14.19.164.1
            """;

        var hops = TraceProbe.Parse(output);

        Assert.Equal(3, hops.Count);
        Assert.True(hops[0].Responded);
        Assert.Equal(1d, hops[0].LatencyMs!.Value);
        Assert.Equal("192.168.1.1", hops[0].Address);
        Assert.False(hops[1].Responded);
        Assert.Null(hops[1].LatencyMs);
        Assert.Equal("14.19.164.1", hops[2].Address);
        Assert.Equal(8d, hops[2].LatencyMs!.Value);
    }

    [Fact]
    public void ParsesLocalizedTimeoutsAndSkipsTheHeader()
    {
        const string output = """
            通过最多 16 个跃点跟踪到 223.5.5.5 的路由

              1     5 ms     5 ms     5 ms  192.168.0.1
              2     *        *        *     请求超时。
            """;

        var hops = TraceProbe.Parse(output);

        Assert.Equal(2, hops.Count);
        Assert.Equal("192.168.0.1", hops[0].Address);
        Assert.False(hops[1].Responded);
    }

    [Fact]
    public void IgnoresLinesThatAreNotHopRows()
    {
        var hops = TraceProbe.Parse("traceroute to 1.1.1.1, 16 hops max\n  1  1.2 ms  1.1 ms  1.0 ms  192.168.1.1");

        Assert.Single(hops);
        Assert.Equal(1, hops[0].Index);
    }
}
