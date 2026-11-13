using System.Text.Json;
using AirNet.Core.Sites;

namespace AirNet.Core.Demo;

/// <summary>
/// A deterministic site-latency run for the screenshot pipeline only.
/// </summary>
/// <remarks>
/// The sites page hydrates from <c>sites.jsonl</c>, so without a record the Store screenshots
/// would show eleven "等待测试" rows. Written into the same throwaway directory that
/// <c>--export</c> creates, never into the user's own history.
///
/// Values follow the expected shape: domestic sites answer in tens of milliseconds, the
/// international ones pay a TLS round trip of several hundred, and all of them succeed —
/// a screenshot is not the place to demonstrate failure states.
/// </remarks>
public static class SampleSites
{
    /// <summary>Per-site timing: DNS, TCP, TLS, TTFB in milliseconds.</summary>
    private static readonly Dictionary<string, (double Dns, double Tcp, double Tls, double Ttfb)> Shape = new()
    {
        ["baidu"] = (1.4, 0.5, 28.9, 9.1),
        ["tencent"] = (1.8, 0.6, 21.0, 32.4),
        ["douyin"] = (2.1, 0.7, 26.5, 18.2),
        ["xiaohongshu"] = (2.4, 0.8, 31.7, 22.6),
        ["bilibili"] = (1.6, 0.6, 24.3, 15.8),
        ["zhihu"] = (2.0, 0.7, 27.4, 19.1),
        ["weibo"] = (1.9, 0.6, 33.6, 25.3),
        ["github"] = (3.2, 1.1, 412.5, 148.7),
        ["google"] = (2.7, 0.9, 268.4, 96.2),
        ["youtube"] = (2.9, 1.0, 301.8, 121.5),
        ["x"] = (3.4, 1.2, 356.2, 133.9),
    };

    /// <summary>One record covering the default catalog, ready to serialise as a history line.</summary>
    public static SiteHistoryRecord Record(DateTimeOffset at) => new()
    {
        At = at,
        Sites = SiteCatalog.Default.Select(site =>
        {
            var (dns, tcp, tls, ttfb) = Shape.TryGetValue(site.Id, out var known) ? known : (2.0, 0.8, 40.0, 20.0);
            return new SiteHistoryEntry
            {
                Id = site.Id,
                Name = site.Name,
                Host = site.Host,
                Group = site.Group.ToString(),
                Success = true,
                DnsMs = dns,
                TcpMs = tcp,
                TlsMs = tls,
                TtfbMs = ttfb,
                HttpStatus = 200,
                FailureKind = nameof(SiteFailureKind.None),
            };
        }).ToList(),
    };

    /// <summary>The same run expressed as the live report shape the page renders.</summary>
    public static SiteLatencyReport Report(DateTimeOffset at)
    {
        var record = Record(at);
        var results = SiteCatalog.Default.Select(site =>
        {
            var entry = record.Sites.First(e => e.Id == site.Id);
            var sample = new SiteSample(
                entry.DnsMs, entry.TcpMs, entry.TlsMs, entry.TtfbMs,
                Success: true, SiteFailureKind.None, FailureNote: null, HttpStatus: entry.HttpStatus);

            return new SiteResult(
                site, [sample], sample, Success: true, SuccessfulSamples: 1,
                SiteFailureKind.None, FailureNote: null, at);
        }).ToList();

        return new SiteLatencyReport(results, at, TimeSpan.FromSeconds(3.6));
    }

    public static async Task WriteAsync(
        string directory,
        DateTimeOffset reference,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var line = JsonSerializer.Serialize(
            Record(reference), DemoHistoryJsonContext.Default.SiteHistoryRecord);

        await File.WriteAllTextAsync(Path.Combine(directory, "sites.jsonl"), line + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }
}
