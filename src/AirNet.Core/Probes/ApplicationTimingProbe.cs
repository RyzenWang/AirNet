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
public static class ApplicationTimingProbe
{
    public const string DefaultHost = "www.baidu.com";

    public static async Task<LayerTiming> MeasureAsync(
        string host = DefaultHost,
        int port = 443,
        int timeoutMs = 4000,
        CancellationToken cancellationToken = default)
    {
        var dnsWatch = Stopwatch.StartNew();
        IPAddress? address;
        try
        {
            var resolved = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            address = resolved.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? resolved.FirstOrDefault();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return Failed(host, 0, 0, 0, 0, "DNS 解析失败");
        }

        dnsWatch.Stop();
        var dnsMs = Round(dnsWatch.Elapsed.TotalMilliseconds);

        if (address is null)
        {
            return Failed(host, dnsMs, 0, 0, 0, "无可用地址");
        }

        using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        var tcpWatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return Failed(host, dnsMs, Round(tcpWatch.Elapsed.TotalMilliseconds), 0, 0, "TCP 连接失败");
        }

        tcpWatch.Stop();
        var tcpMs = Round(tcpWatch.Elapsed.TotalMilliseconds);

        await using var network = new NetworkStream(socket, ownsSocket: false);
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
            return Failed(host, dnsMs, tcpMs, Round(tlsWatch.Elapsed.TotalMilliseconds), 0, "TLS 握手失败");
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
                return Failed(host, dnsMs, tcpMs, tlsMs, 0, "空响应");
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
                HttpStatus: status);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            return Failed(host, dnsMs, tcpMs, tlsMs, 0, "首包读取失败");
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
        string host, double dnsMs, double tcpMs, double tlsMs, double ttfbMs, string note) =>
        new(host, dnsMs, tcpMs, tlsMs, ttfbMs, false, note);

    private static double Round(double value) => Math.Round(value, 1);
}
