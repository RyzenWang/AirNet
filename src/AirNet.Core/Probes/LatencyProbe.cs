using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AirNet.Core.Probes;

/// <summary>
/// Collects a latency series against a host. ICMP is tried first; when the network (or a
/// captive firewall) drops echo requests, a TCP connect to a common port is used instead.
/// Both are unprivileged on Windows and fall back cleanly on other platforms.
/// </summary>
public static class LatencyProbe
{
    public static async Task<IReadOnlyList<double?>> MeasureIcmpAsync(
        string host,
        int count = 20,
        int intervalMs = 120,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default)
    {
        var samples = new List<double?>(count);
        using var ping = new Ping();

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
                samples.Add(reply.Status == IPStatus.Success
                    ? Math.Round((double)reply.RoundtripTime, 1)
                    : null);
            }
            catch (Exception ex) when (ex is PingException or SocketException or InvalidOperationException)
            {
                samples.Add(null);
            }

            if (i < count - 1)
            {
                await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        return samples;
    }

    /// <summary>TCP connect time to a port; used when ICMP is filtered.</summary>
    public static async Task<double?> MeasureTcpAsync(
        string host,
        int port = 443,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
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
