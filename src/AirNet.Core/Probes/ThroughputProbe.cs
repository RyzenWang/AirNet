using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Single-connection bandwidth probes, one per direction. Both report what the connection
/// actually carried — no ramp-up exclusion, no multi-stream extrapolation — because a number
/// that needs a caveat is worse than an honest conservative one.
/// </summary>
public static class ThroughputProbe
{
    /// <summary>
    /// Measures downlink for a fixed time budget rather than a fixed byte count. A byte target
    /// either finishes too early on fast links or never finishes on slow ones — the latter is
    /// exactly how the first version reported 0 Mbps on a 7 Mbps line.
    /// </summary>
    public static async Task<double> MeasureDownloadMbpsAsync(
        string url,
        int maxBytes = 25 * 1024 * 1024,
        int maxDurationMs = 4000,
        CancellationToken cancellationToken = default)
    {
        using var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(maxDurationMs + 8000) };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
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

            while (total < maxBytes && stopwatch.ElapsedMilliseconds < maxDurationMs)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
                                       or System.Net.Sockets.SocketException
                                       or TaskCanceledException or OperationCanceledException)
        {
            // A connection dropped mid-transfer still tells us how fast those bytes arrived,
            // which beats discarding the sample and showing 0.
            stopwatch.Stop();
            return SpeedMath.Mbps(total, stopwatch.Elapsed);
        }

        stopwatch.Stop();
        return SpeedMath.Mbps(total, stopwatch.Elapsed);
    }

    /// <summary>
    /// Uploads a fixed number of bytes and times it end to end. The body is streamed from one
    /// reused buffer, so an 8 MB test never allocates 8 MB.
    /// </summary>
    public static async Task<double> MeasureUploadMbpsAsync(
        string url,
        int bytes = 8 * 1024 * 1024,
        int timeoutMs = 20000,
        CancellationToken cancellationToken = default)
    {
        using var handler = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
        using var content = new StreamedBytesContent(bytes);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

        var stopwatch = Stopwatch.StartNew();
        long sent;
        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            sent = content.BytesWritten;
            stopwatch.Stop();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException
                                       or System.Net.Sockets.SocketException
                                       or TaskCanceledException or OperationCanceledException)
        {
            // Upload cannot be salvaged from a partial body: the server rejects it.
            return 0;
        }

        return SpeedMath.Mbps(sent, stopwatch.Elapsed);
    }

    /// <summary>Writes <paramref name="totalBytes"/> zeroes without buffering the whole payload.</summary>
    private sealed class StreamedBytesContent : HttpContent
    {
        private readonly int _totalBytes;

        public StreamedBytesContent(int totalBytes)
        {
            _totalBytes = totalBytes;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        /// <summary>Counted as the transport consumes the body, so a partial send is not counted as success.</summary>
        public long BytesWritten { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[64 * 1024];
            var remaining = _totalBytes;
            while (remaining > 0)
            {
                var chunk = Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, chunk), CancellationToken.None).ConfigureAwait(false);
                remaining -= chunk;
                BytesWritten += chunk;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return true;
        }
    }
}
