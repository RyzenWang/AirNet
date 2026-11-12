using AirNet.Core.Diagnostics;
using AirNet.Core.Probes;
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

    [Theory]
    [InlineData(200, SiteFailureKind.None)]
    [InlineData(403, SiteFailureKind.HttpBlocked)]
    [InlineData(429, SiteFailureKind.HttpBlocked)]
    [InlineData(503, SiteFailureKind.HttpServerError)]
    public void HttpStatusTurnsAReachableResponseIntoAnHttpIssue(int status, SiteFailureKind expected)
    {
        var sample = SiteSample.FromTiming(
            new LayerTiming("example.com", 1, 2, 3, 4, true, HttpStatus: status));

        Assert.True(sample.Success);
        Assert.Equal(expected, sample.FailureKind);
        Assert.Equal(status == 200 ? null : $"HTTP {status}", sample.FailureNote);
        Assert.Equal(status, sample.HttpStatus);
    }

    [Fact]
    public async Task HttpBlockedResponseIsNotCountedAsASuccessfulSite()
    {
        var site = new SiteTarget("blocked", "Blocked", "blocked.example", SiteGroup.Custom, true);
        var report = await SiteLatencyProbe.RunAsync(
            [site],
            samplesPerSite: 1,
            measure: (_, _) => Task.FromResult(
                new LayerTiming("blocked.example", 1, 2, 3, 4, true, HttpStatus: 403)));

        var result = Assert.Single(report.Results);
        Assert.True(result.Success);
        Assert.Equal(SiteFailureKind.HttpBlocked, result.FailureKind);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal("HTTP 403", result.FailureNote);
        Assert.Equal(0, report.Succeeded);
    }

    [Theory]
    [InlineData("HTTP/1.1 200 OK\r\n", 200)]
    [InlineData("HTTP/1.0 403 Forbidden\r\n", 403)]
    [InlineData("HTTP/2 503 Service Unavailable\r\n", 503)]
    public void ParsesHttpStatusLine(string response, int expected)
    {
        Assert.Equal(expected, ApplicationTimingProbe.ParseStatus(System.Text.Encoding.ASCII.GetBytes(response)));
    }

    [Fact]
    public void IncompleteStatusLineIsNotGuessed()
    {
        Assert.Null(ApplicationTimingProbe.ParseStatus("HTTP/1.1 2"u8));
        Assert.Null(ApplicationTimingProbe.ParseStatus("not http"u8));
    }

    [Fact]
    public async Task SiteHistoryRoundTripsAndKeepsHttpStatus()
    {
        var directory = Path.Combine(Path.GetTempPath(), "airnet-site-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SiteHistoryStore(directory);
            var at = DateTimeOffset.Now;
            var successSample = new SiteSample(4, 8, 12, 16, true, SiteFailureKind.None, null, 200);
            var blockedSample = new SiteSample(5, 9, 13, 17, true, SiteFailureKind.HttpBlocked, "HTTP 403", 403);
            var failedSample = new SiteSample(0, 0, 0, 0, false, SiteFailureKind.Tcp, "TCP 连接失败");
            var results = new[]
            {
                new SiteResult(SiteCatalog.Default[0], [successSample], successSample, true, 1, SiteFailureKind.None, null, at),
                new SiteResult(SiteCatalog.Default[1], [blockedSample], blockedSample, true, 1, SiteFailureKind.HttpBlocked, "HTTP 403", at),
                new SiteResult(SiteCatalog.Default[2], [], null, false, 0, SiteFailureKind.Tcp, "TCP 连接失败", at),
            };

            await store.AppendAsync(new SiteLatencyReport(results, at, TimeSpan.FromSeconds(2)));
            var loaded = await store.LoadLatestAsync();

            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.Results.Count);
            Assert.Equal(403, loaded.Results[1].HttpStatus);
            Assert.Equal(SiteFailureKind.HttpBlocked, loaded.Results[1].FailureKind);
            Assert.False(loaded.Results[2].Success);
            Assert.Equal(1, loaded.Succeeded);
            Assert.True(File.Exists(store.FilePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
