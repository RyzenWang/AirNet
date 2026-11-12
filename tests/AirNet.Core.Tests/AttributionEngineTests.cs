using AirNet.Core.Demo;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Tests;

public sealed class AttributionEngineTests
{
    [Fact]
    public void ALastRouterIsNotBlamedAsTheFarEndWhenTheTargetNeverAnswered()
    {
        var trace = new TraceResult(
            "223.5.5.5",
            [
                new TraceHop(1, "192.168.1.1", 2, true),
                new TraceHop(2, "14.19.164.1", 10, true),
                new TraceHop(3, "*", null, false),
            ],
            Succeeded: true,
            ReachedHop: 2,
            ReachedTarget: false);

        var attribution = AttributionEngine.FromTrace(DemoSnapshotFactory.CreateSnapshot(), trace);

        Assert.NotEqual("对端", attribution.Culprit);
        Assert.Contains("未响应", attribution.Detail);
        Assert.Contains(attribution.Segments, s => s.Name == "对端" && s.LatencyText == "未测");
    }

    [Fact]
    public void AReachedTargetWithALargeLastHopIsTheFarEnd()
    {
        var trace = new TraceResult(
            "223.5.5.5",
            [
                new TraceHop(1, "192.168.1.1", 2, true),
                new TraceHop(2, "14.19.164.1", 10, true),
                new TraceHop(3, "223.5.5.5", 100, true),
            ],
            Succeeded: true,
            ReachedHop: 3,
            ReachedTarget: true);

        var attribution = AttributionEngine.FromTrace(DemoSnapshotFactory.CreateSnapshot(), trace);

        Assert.Equal("对端", attribution.Culprit);
        Assert.Contains("最后一跳", attribution.Detail);
    }

    [Fact]
    public void AnUnresolvedTargetKeepsTheOlderLastHopBehaviour()
    {
        var trace = new TraceResult(
            "example.invalid",
            [
                new TraceHop(1, "192.168.1.1", 2, true),
                new TraceHop(2, "203.0.113.9", 100, true),
            ],
            Succeeded: true,
            ReachedHop: 2,
            ReachedTarget: null);

        var attribution = AttributionEngine.FromTrace(DemoSnapshotFactory.CreateSnapshot(), trace);

        Assert.Equal("对端", attribution.Culprit);
    }
}
