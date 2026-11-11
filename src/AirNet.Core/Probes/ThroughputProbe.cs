using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Single-connection ranged download used for the bandwidth estimate. Deliberately simple: the
/// prototype only needs a representative number, and the production design uses parallel
/// streams with a discard sink (see TECH_PLAN.md, 带宽测速).
/// </summary>
public static class ThroughputProbe
{
    public static async Task<double> MeasureDownloadMbpsAsync(
        string url,
        int bytes = 12 * 1024 * 1024,
        int timeoutMs = 8000,
        CancellationToken cancellationToken = default)
    {
        using var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, bytes - 1);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        var buffer = new byte[64 * 1024];
        long total = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            while (total < bytes)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            stopwatch.Stop();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return 0;
        }

        var seconds = stopwatch.Elapsed.TotalSeconds;
        return seconds <= 0 || total == 0 ? 0 : Math.Round(total * 8 / seconds / 1_000_000, 1);
    }
}
