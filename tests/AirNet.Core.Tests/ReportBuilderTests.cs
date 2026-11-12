using AirNet.Core.Demo;
using AirNet.Core.Diagnostics;
using AirNet.Core.Reporting;
using AirNet.Core.Sites;

namespace AirNet.Core.Tests;

public sealed class ReportBuilderTests
{
    [Theory]
    [InlineData("192.168.1.42", "192.168.x.x")]
    [InlineData("10.0.0.9", "10.0.x.x")]
    [InlineData("172.16.5.4", "172.16.x.x")]
    [InlineData("203.0.113.42", "203.0.113.x")]
    [InlineData("未获取", "未获取")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void MaskKeepsPrivateRangesAndTrimsPublicAddresses(string input, string expected)
    {
        Assert.Equal(expected, ReportBuilder.Mask(input, mask: true));
    }

    [Fact]
    public void MaskCanBeDisabled()
    {
        Assert.Equal("192.168.1.42", ReportBuilder.Mask("192.168.1.42", mask: false));
    }

    [Fact]
    public void ReportIsSelfContained()
    {
        var html = ReportBuilder.Build(DemoSnapshotFactory.CreateSnapshot());

        Assert.Contains("<svg", html);
        Assert.Contains("class=\"metric\"", html);
        Assert.Contains("<footer>", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public void UnmeasuredBandwidthIsNotRenderedAsZero()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot() with
        {
            DownloadMbps = 0,
            UploadMbps = 0,
        };

        var html = ReportBuilder.Build(snapshot);

        Assert.Contains("未测速", html);
        Assert.DoesNotContain(">0<span class=\"unit\">Mbps", html);
    }

    [Fact]
    public void UnknownEgressAddressOmitsTheChip()
    {
        var snapshot = DemoSnapshotFactory.CreateSnapshot() with { EgressAddress = string.Empty };

        var html = ReportBuilder.Build(snapshot);

        Assert.DoesNotContain("出口 未获取", html);
    }

    [Fact]
    public void SiteLatencyTableIsAppendedOnlyWhenProvided()
    {
        var sample = new SiteSample(4, 8, 16, 2, true, SiteFailureKind.None, null);
        var result = new SiteResult(
            SiteCatalog.Default[0], [sample], sample, true, 1, SiteFailureKind.None, null, DateTimeOffset.Now);
        var siteReport = new SiteLatencyReport([result], DateTimeOffset.Now, TimeSpan.FromSeconds(2));

        var withSites = ReportBuilder.Build(DemoSnapshotFactory.CreateSnapshot(), siteLatency: siteReport);
        var withoutSites = ReportBuilder.Build(DemoSnapshotFactory.CreateSnapshot());

        Assert.Contains("网站延迟", withSites);
        Assert.Contains("百度", withSites);
        Assert.DoesNotContain("网站延迟", withoutSites);
        Assert.DoesNotContain("<script", withSites);
    }
}
