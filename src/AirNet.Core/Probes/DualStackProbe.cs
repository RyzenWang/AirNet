using System.Net;
using System.Net.Sockets;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Times the same host over IPv4 and IPv6 with fresh TCP handshakes. Targets a hostname, not a
/// literal: dual-stack only matters for names, which is how users actually reach things.
/// </summary>
public static class DualStackProbe
{
    public static async Task<DualStackResult> MeasureAsync(
        string host,
        int port = 443,
        int samples = 5,
        int timeoutMs = 1200,
        CancellationToken cancellationToken = default)
    {
        IPAddress? v4;
        IPAddress? v6;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            v4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            v6 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return DualStackResult.Unavailable;
        }

        if (v4 is null && v6 is null)
        {
            return DualStackResult.Unavailable;
        }

        var v4Task = ProbeAsync(v4, port, samples, timeoutMs, cancellationToken);
        var v6Task = ProbeAsync(v6, port, samples, timeoutMs, cancellationToken);
        await Task.WhenAll(v4Task, v6Task).ConfigureAwait(false);

        var (v4Ms, v4Count) = v4Task.Result;
        var (v6Ms, v6Count) = v6Task.Result;

        return DualStackAnalyzer.Evaluate(v4Ms, v6Ms, v4Count, v6Count);
    }

    private static async Task<(double? MedianMs, int Received)> ProbeAsync(
        IPAddress? address,
        int port,
        int samples,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        if (address is null)
        {
            return (null, 0);
        }

        var endpoint = new IPEndPoint(address, port);
        var results = new List<double?>(samples);

        for (var i = 0; i < samples; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await LatencyProbe
                .MeasureTcpConnectAsync(endpoint, timeoutMs, cancellationToken)
                .ConfigureAwait(false));

            if (i < samples - 1)
            {
                await Task.Delay(40, cancellationToken).ConfigureAwait(false);
            }
        }

        var stats = LatencyAnalyzer.Analyze(results);
        return (stats.Received > 0 ? stats.MedianMs : null, stats.Received);
    }
}
