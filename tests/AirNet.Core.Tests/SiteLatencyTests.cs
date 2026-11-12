using AirNet.Core.Diagnostics;
using AirNet.Core.Sites;

namespace AirNet.Core.Tests;

public sealed class SiteLatencyTests
{
    [Fact]
    public void CatalogHasSevenDomesticAndFourInternationalSites()
    {
        Assert.Equal(7, SiteCatalog.Default.Count(site => site.Group == SiteGroup.Domestic));
        Assert.Equal(4, SiteCatalog.Default.Count(site => site.Group == SiteGroup.International));
        Assert.Equal(11, SiteCatalog.Default.Count);
        Assert.Equal(11, SiteCatalog.Default.Select(site => site.Host).Distinct().Count());
    }

    [Theory]
    [InlineData("https://example.com", "example.com")]
    [InlineData("https://example.com/path?q=1", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("www.example.com:443", "www.example.com")]
    public void NormalizesWebsiteAddresses(string input, string expected)
    {
        Assert.True(CustomSiteRules.TryNormalizeHost(input, out var host, out var error), error);
        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com:8443")]
    [InlineData("127.0.0.1")]
    [InlineData("not a host")]
    public void RejectsNonHttpsOrNonHostnames(string input)
    {
        Assert.False(CustomSiteRules.TryNormalizeHost(input, out _, out _));
    }

    [Fact]
    public void RejectsDuplicateHosts()
    {
        var existing = SiteCatalog.Default.ToList();
        Assert.NotNull(CustomSiteRules.ValidateDuplicate("WWW.BAIDU.COM", existing));
        Assert.Null(CustomSiteRules.ValidateDuplicate("status.example.com", existing));
    }

    [Fact]
    public void CustomStoreRoundTripsAndSurvivesCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), "airnet-sites-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CustomSiteStore(directory);
            store.Save(
            [
                new SiteTarget("one", "公司官网", "status.example.com", SiteGroup.Custom, true),
                new SiteTarget("two", "内部工具", "tools.example.com", SiteGroup.Custom, true),
            ]);

            var loaded = store.Load();
            Assert.Equal(2, loaded.Count);
            Assert.All(loaded, site => Assert.True(site.IsCustom));

            File.WriteAllText(store.FilePath, "{ broken json");
            Assert.Empty(store.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MedianSampleWinsAndKeepsFourSegmentsAdditive()
    {
        var site = new SiteTarget("example", "Example", "example.com", SiteGroup.Custom, true);
        var sample = 0;

        var report = await SiteLatencyProbe.RunAsync(
            [site],
            samplesPerSite: 3,
            concurrency: 1,
            measure: (_, _) =>
            {
                sample++;
                return Task.FromResult(new LayerTiming("example.com", sample * 10, 0, 0, 0, true));
            });

        var result = Assert.Single(report.Results);
        Assert.True(result.Success);
        Assert.Equal(3, result.SuccessfulSamples);
        Assert.Equal(20, result.TotalMs);
        Assert.Equal(20, result.Median!.TotalMs);
    }

    [Fact]
    public async Task FailedSamplesAreClassifiedWithoutThrowing()
    {
        var site = new SiteTarget("broken", "Broken", "broken.example", SiteGroup.Custom, true);

        var report = await SiteLatencyProbe.RunAsync(
            [site],
            samplesPerSite: 2,
            measure: (_, _) => Task.FromResult(
                new LayerTiming("broken.example", 0, 0, 0, 0, false, "DNS 解析失败")));

        var result = Assert.Single(report.Results);
        Assert.False(result.Success);
        Assert.Equal(0, result.SuccessfulSamples);
        Assert.Equal(SiteFailureKind.Dns, result.FailureKind);
        Assert.Equal("DNS 解析失败", result.FailureNote);
    }

    [Theory]
    [InlineData("DNS 解析失败", SiteFailureKind.Dns)]
    [InlineData("TCP 连接失败", SiteFailureKind.Tcp)]
    [InlineData("TLS 握手失败", SiteFailureKind.Tls)]
    [InlineData("首包读取失败", SiteFailureKind.Ttfb)]
    [InlineData("", SiteFailureKind.Unknown)]
    public void ClassifiesFailureNotes(string note, SiteFailureKind expected)
    {
        Assert.Equal(expected, SiteLatencyProbe.ClassifyFailure(note, success: false));
        Assert.Equal(SiteFailureKind.None, SiteLatencyProbe.ClassifyFailure(note, success: true));
    }
}
