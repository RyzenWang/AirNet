using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AirNet.Core.Probes;

/// <summary>
/// Collects latency series. ICMP is tried first; when the network (or a captive firewall) drops
/// echo requests, TCP connect times are used instead.
/// </summary>
/// <remarks>
/// ICMP through <see cref="Ping"/> only reports whole milliseconds, so a 20-packet run often
/// comes back as twenty identical values and the jitter reads as zero. TCP handshakes are timed
/// with <see cref="Stopwatch"/>, which keeps sub-millisecond resolution; that series is what the
/// jitter and P95 numbers are built from.
/// </remarks>
public static class LatencyProbe
{
    public static async Task<IReadOnlyList<double?>> MeasureIcmpAsync(
        string host,
        int count = 20,
        int intervalMs = 120,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default,
        Action<double?>? onSample = null)
    {
        var samples = new List<double?>(count);
        using var ping = new Ping();

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double? sample;
            try
            {
                var reply = await ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
                sample = reply.Status == IPStatus.Success
                    ? Math.Round((double)reply.RoundtripTime, 2)
                    : null;
            }
            catch (Exception ex) when (ex is PingException or SocketException or InvalidOperationException)
            {
                sample = null;
            }

            samples.Add(sample);
            onSample?.Invoke(sample);

            if (i < count - 1)
            {
                await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        return samples;
    }

    /// <summary>
    /// A series of fresh TCP handshakes. Each sample opens its own socket, so every number is a
    /// real SYN → SYN/ACK round trip rather than a cached connection.
    /// </summary>
    public static async Task<IReadOnlyList<double?>> MeasureTcpSeriesAsync(
        string host,
        int port = 53,
        int count = 12,
        int intervalMs = 80,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default,
        Action<double?>? onSample = null)
    {
        var samples = new List<double?>(count);

        IPAddress? address;
        try
        {
            var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            address = resolved.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? resolved.FirstOrDefault();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            address = null;
        }

        if (address is null)
        {
            for (var i = 0; i < count; i++)
            {
                samples.Add(null);
            }

            return samples;
        }

        var endpoint = new IPEndPoint(address, port);

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = await MeasureTcpConnectAsync(endpoint, timeoutMs, cancellationToken).ConfigureAwait(false);
            samples.Add(sample);
            onSample?.Invoke(sample);

            if (i < count - 1)
            {
                await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        return samples;
    }

    /// <summary>One handshake, returns milliseconds or null on failure.</summary>
    public static async Task<double?> MeasureTcpAsync(
        string host,
        int port = 443,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            var address = resolved.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                          ?? resolved.FirstOrDefault();
            if (address is null)
            {
                return null;
            }

            return await MeasureTcpConnectAsync(new IPEndPoint(address, port), timeoutMs, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<double?> MeasureTcpConnectAsync(
        IPEndPoint endpoint,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await socket.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Resolves a host and reports how long the lookup took.</summary>
    public static async Task<double> MeasureDnsAsync(string host, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return addresses.Length == 0 ? 0 : Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return 0;
        }
    }
}
