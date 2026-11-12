using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Times the four segments a first HTTPS request spends its time in: DNS, TCP, TLS and
/// time-to-first-byte. Doing it on one socket in order is what makes the numbers additive and
/// lets the diagnosis say "slow DNS" instead of just "slow".
/// </summary>
/// <remarks>
/// When a proxy applies, the TCP segment covers connecting to the proxy and the CONNECT
/// handshake — the proxy only answers 200 once it has its own connection to the site, so that
/// span is the real "path established" cost. DNS stays local so the resolver can still be judged.
/// </remarks>
public static class ApplicationTimingProbe
{
    public const string DefaultHost = "www.baidu.com";

    public static async Task<LayerTiming> MeasureAsync(
        string host = DefaultHost,
        int port = 443,
        int timeoutMs = 4000,
        IWebProxy? proxy = null,
        CancellationToken cancellationToken = default)
    {
        var proxyEndpoint = ProxyTunnel.Choose(proxy, host, port);
        var viaProxy = proxyEndpoint is not null;

        var dnsWatch = Stopwatch.StartNew();
        IPAddress? address = null;
        try
        {
            var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            address = resolved.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? resolved.FirstOrDefault();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            // A proxied request does not strictly need local DNS; the proxy resolves the name.
            if (!viaProxy)
            {
                return Failed(host, 0, 0, 0, 0, "DNS 解析失败", viaProxy);
            }
        }

        dnsWatch.Stop();
        var dnsMs = Round(dnsWatch.Elapsed.TotalMilliseconds);

        if (!viaProxy && address is null)
        {
            return Failed(host, dnsMs, 0, 0, 0, "无可用地址", viaProxy);
        }

        // Resolve the proxy host (it is usually a literal, so this is normally instant).
        EndPoint targetEndPoint;
        if (proxyEndpoint is not null)
        {
            IPAddress? proxyAddress;
            try
            {
                var resolved = await Dns.GetHostAddressesAsync(proxyEndpoint.Host, cancellationToken)
                    .ConfigureAwait(false);
                proxyAddress = resolved.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                               ?? resolved.FirstOrDefault();
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                proxyAddress = null;
            }

            if (proxyAddress is null)
            {
                return Failed(host, dnsMs, 0, 0, 0, "代理地址不可达", viaProxy);
            }

            targetEndPoint = new IPEndPoint(proxyAddress, proxyEndpoint.Port);
        }
        else
        {
            targetEndPoint = new IPEndPoint(address!, port);
        }

        using var socket = new Socket(targetEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        var tcpWatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await socket.ConnectAsync(targetEndPoint, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            var what = viaProxy ? "代理连接失败" : "TCP 连接失败";
            return Failed(host, dnsMs, Round(tcpWatch.Elapsed.TotalMilliseconds), 0, 0, what, viaProxy);
        }

        await using var network = new NetworkStream(socket, ownsSocket: false);

        // The CONNECT exchange counts toward the TCP segment: it is where the path is built.
        if (proxyEndpoint is not null)
        {
            using var tunnelTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tunnelTimeout.CancelAfter(timeoutMs);
            if (!await ProxyTunnel.EstablishAsync(network, host, port, tunnelTimeout.Token).ConfigureAwait(false))
            {
                return Failed(host, dnsMs, Round(tcpWatch.Elapsed.TotalMilliseconds), 0, 0, "代理隧道建立失败", viaProxy);
            }
        }

        tcpWatch.Stop();
        var tcpMs = Round(tcpWatch.Elapsed.TotalMilliseconds);

        using var secure = new SslStream(network, leaveInnerStreamOpen: true);

        var tlsWatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await secure.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    EnabledSslProtocols = SslProtocols.None,
                },
                timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or OperationCanceledException)
        {
            return Failed(host, dnsMs, tcpMs, Round(tlsWatch.Elapsed.TotalMilliseconds), 0, "TLS 握手失败", viaProxy);
        }

        tlsWatch.Stop();
        var tlsMs = Round(tlsWatch.Elapsed.TotalMilliseconds);

        var request = Encoding.ASCII.GetBytes(
            $"GET / HTTP/1.1\r\nHost: {host}\r\nUser-Agent: AirNet/0.1\r\nAccept: */*\r\nConnection: close\r\n\r\n");

        var ttfbWatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await secure.WriteAsync(request, timeout.Token).ConfigureAwait(false);

            var buffer = new byte[256];
            var read = await secure.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                return Failed(host, dnsMs, tcpMs, tlsMs, 0, "空响应", viaProxy);
            }

            ttfbWatch.Stop();
            var status = ParseStatus(buffer.AsSpan(0, read));
            return new LayerTiming(
                host,
                dnsMs,
                tcpMs,
                tlsMs,
                Round(ttfbWatch.Elapsed.TotalMilliseconds),
                true,
                HttpStatus: status,
                ViaProxy: viaProxy);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            return Failed(host, dnsMs, tcpMs, tlsMs, 0, "首包读取失败", viaProxy);
        }
    }

    /// <summary>Reads the numeric code from "HTTP/1.1 200 OK"; null when the chunk is incomplete.</summary>
    internal static int? ParseStatus(ReadOnlySpan<byte> data)
    {
        var text = Encoding.ASCII.GetString(data);
        var line = text.Split('\n')[0].TrimEnd('\r');
        if (!line.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
               && int.TryParse(parts[1], out var status)
               && status is >= 100 and <= 599
            ? status
            : null;
    }

    private static LayerTiming Failed(
        string host,
        double dnsMs,
        double tcpMs,
        double tlsMs,
        double ttfbMs,
        string note,
        bool viaProxy = false) =>
        new(host, dnsMs, tcpMs, tlsMs, ttfbMs, false, note, null, viaProxy);

    private static double Round(double value) => Math.Round(value, 1);
}
