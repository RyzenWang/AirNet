using AirNet.Core.Diagnostics;
using AirNet.Core.Probes;

namespace AirNet.Core.Sites;

public enum SiteFailureKind
{
    None,
    Dns,
    Tcp,
    Tls,
    Ttfb,
    Cancelled,
    Unknown,
}

/// <summary>One HTTPS sample, split into the four segments that add up to the total.</summary>
public sealed record SiteSample(
    double DnsMs,
    double TcpMs,
    double TlsMs,
    double TtfbMs,
    bool Success,
    SiteFailureKind FailureKind,
    string? FailureNote)
{
    public double TotalMs => Math.Round(DnsMs + TcpMs + TlsMs + TtfbMs, 1);

    public static SiteSample FromTiming(LayerTiming timing) => new(
        timing.DnsMs,
        timing.TcpMs,
        timing.TlsMs,
        timing.TtfbMs,
        timing.Succeeded,
        SiteLatencyProbe.ClassifyFailure(timing.FailureNote, timing.Succeeded),
        timing.FailureNote);

    public static SiteSample Cancelled() =>
        new(0, 0, 0, 0, false, SiteFailureKind.Cancelled, "已取消");
}

/// <summary>The median sample for one site, plus enough context to render success and failure rows.</summary>
public sealed record SiteResult(
    SiteTarget Target,
    IReadOnlyList<SiteSample> Samples,
    SiteSample? Median,
    bool Success,
    int SuccessfulSamples,
    SiteFailureKind FailureKind,
    string? FailureNote,
    DateTimeOffset At)
{
    public double TotalMs => Median?.TotalMs ?? 0;
}

public sealed record SiteLatencyReport(
    IReadOnlyList<SiteResult> Results,
    DateTimeOffset StartedAt,
    TimeSpan Duration)
{
    public int Succeeded => Results.Count(result => result.Success);
}

/// <summary>
/// Runs the site catalog with limited concurrency and picks the median total sample per site.
/// The actual four-segment measurement is delegated to <see cref="ApplicationTimingProbe"/> by
/// default, which keeps the probe reusable and makes the aggregation easy to test.
/// </summary>
public static class SiteLatencyProbe
{
    public static async Task<SiteLatencyReport> RunAsync(
        IReadOnlyList<SiteTarget> sites,
        int samplesPerSite = 3,
        int concurrency = 4,
        int timeoutMs = 4000,
        IProgress<SiteResult>? progress = null,
        CancellationToken cancellationToken = default,
        Func<SiteTarget, CancellationToken, Task<LayerTiming>>? measure = null)
    {
        var started = DateTimeOffset.Now;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var results = new SiteResult[sites.Count];

        if (sites.Count == 0)
        {
            return new SiteLatencyReport([], started, TimeSpan.Zero);
        }

        samplesPerSite = Math.Max(1, samplesPerSite);
        concurrency = Math.Clamp(concurrency, 1, sites.Count);
        measure ??= (site, ct) => ApplicationTimingProbe.MeasureAsync(
            site.Host,
            timeoutMs: timeoutMs,
            cancellationToken: ct);

        using var gate = new SemaphoreSlim(concurrency, concurrency);
        var tasks = sites.Select(async (site, index) =>
        {
            if (!await TryEnterAsync(gate, cancellationToken).ConfigureAwait(false))
            {
                results[index] = Cancelled(site);
                return;
            }

            try
            {
                var result = await MeasureSiteAsync(
                    site, samplesPerSite, timeoutMs, measure, cancellationToken).ConfigureAwait(false);
                results[index] = result;
                progress?.Report(result);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        watch.Stop();
        return new SiteLatencyReport(results, started, watch.Elapsed);
    }

    private static async Task<SiteResult> MeasureSiteAsync(
        SiteTarget site,
        int samplesPerSite,
        int timeoutMs,
        Func<SiteTarget, CancellationToken, Task<LayerTiming>> measure,
        CancellationToken cancellationToken)
    {
        var samples = new List<SiteSample>(samplesPerSite);

        for (var i = 0; i < samplesPerSite; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                samples.Add(SiteSample.Cancelled());
                break;
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(timeoutMs);
                var timing = await measure(site, timeout.Token).ConfigureAwait(false);
                samples.Add(SiteSample.FromTiming(timing));
            }
            catch (OperationCanceledException)
            {
                samples.Add(SiteSample.Cancelled());
                break;
            }
            catch (Exception ex)
            {
                samples.Add(new SiteSample(
                    0, 0, 0, 0, false, SiteFailureKind.Unknown, ex.Message));
                break;
            }
        }

        var successful = samples.Where(sample => sample.Success).ToList();
        SiteSample? median = null;
        if (successful.Count > 0)
        {
            var ordered = successful.OrderBy(sample => sample.TotalMs).ToList();
            median = ordered[ordered.Count / 2];
        }

        var failure = median is null
            ? samples.FirstOrDefault(sample => !sample.Success)
            : null;

        return new SiteResult(
            site,
            samples,
            median,
            median is not null,
            successful.Count,
            failure?.FailureKind ?? SiteFailureKind.None,
            failure?.FailureNote,
            DateTimeOffset.Now);
    }

    private static async Task<bool> TryEnterAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static SiteResult Cancelled(SiteTarget site) => new(
        site,
        [SiteSample.Cancelled()],
        null,
        false,
        0,
        SiteFailureKind.Cancelled,
        "已取消",
        DateTimeOffset.Now);

    internal static SiteFailureKind ClassifyFailure(string? note, bool success)
    {
        if (success)
        {
            return SiteFailureKind.None;
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return SiteFailureKind.Unknown;
        }

        if (note.Contains("DNS", StringComparison.OrdinalIgnoreCase))
        {
            return SiteFailureKind.Dns;
        }

        if (note.Contains("TCP", StringComparison.OrdinalIgnoreCase))
        {
            return SiteFailureKind.Tcp;
        }

        if (note.Contains("TLS", StringComparison.OrdinalIgnoreCase))
        {
            return SiteFailureKind.Tls;
        }

        if (note.Contains("首包", StringComparison.Ordinal) || note.Contains("空响应", StringComparison.Ordinal))
        {
            return SiteFailureKind.Ttfb;
        }

        if (note.Contains("取消", StringComparison.Ordinal))
        {
            return SiteFailureKind.Cancelled;
        }

        return SiteFailureKind.Unknown;
    }
}
