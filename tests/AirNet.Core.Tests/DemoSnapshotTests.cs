using AirNet.Core.Demo;

namespace AirNet.Core.Tests;

public sealed class DemoSnapshotTests
{
    [Fact]
    public void SampleStartsWithNoBandwidth()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot();

        Assert.True(snapshot.IsSample);
        Assert.False(snapshot.HasBandwidth);
        Assert.False(snapshot.HasUpload);
    }

    [Fact]
    public void SampleStillAcceptsExplicitMeasuredBandwidth()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot(downloadMbps: 384.2, uploadMbps: 92.6);

        Assert.True(snapshot.HasBandwidth);
        Assert.Equal(384.2, snapshot.DownloadMbps);
        Assert.Equal(92.6, snapshot.UploadMbps);
    }
}
