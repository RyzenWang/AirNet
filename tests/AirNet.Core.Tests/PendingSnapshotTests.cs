using AirNet.Core.Diagnostics;

namespace AirNet.Core.Tests;

public sealed class PendingSnapshotTests
{
    [Fact]
    public void PendingSnapshotHasNoMeasuredValues()
    {
        var pending = PendingSnapshot.Create();

        Assert.True(pending.IsPending);
        Assert.False(pending.IsSample);
        Assert.False(pending.HasBandwidth);
        Assert.Equal(0, pending.Latency.Sent);
        Assert.Equal(0, pending.Latency.Received);
        Assert.Empty(pending.LatencySeries);
        Assert.Empty(pending.Hops);
        Assert.Empty(pending.Scenarios);
        Assert.Empty(pending.Findings);
    }
}
