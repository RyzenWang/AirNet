using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AirNet.Core.Probes;

/// <summary>Where an HTTP proxy lives, when one applies to a given target.</summary>
public sealed record ProxyEndpoint(string Host, int Port);

/// <summary>
/// HTTP CONNECT tunnelling for the raw-socket timing probe.
/// </summary>
/// <remarks>
/// The rest of the app talks through <see cref="HttpClient"/>, which honours the system proxy on
/// its own. The four-segment timing probe needs its own socket to keep DNS/TCP/TLS/TTFB
/// separable, so it has to open the tunnel by hand — otherwise the site latency page reports
/// numbers for a path the browser never takes.
/// </remarks>
public static class ProxyTunnel
{
    /// <summary>
    /// Null means "connect directly". A proxy that is bypassed for this host, or that resolves to
    /// the target itself, is treated as no proxy.
    /// </summary>
    public static ProxyEndpoint? Choose(IWebProxy? proxy, string host, int port)
    {
        if (proxy is null)
        {
            return null;
        }

        Uri target;
        try
        {
            target = new Uri($"https://{host}:{port}/");
        }
        catch (UriFormatException)
        {
            return null;
        }

        try
        {
            if (proxy.IsBypassed(target))
            {
                return null;
            }

            var destination = proxy.GetProxy(target);
            if (destination is null)
            {
                return null;
            }

            if (string.Equals(destination.Host, host, StringComparison.OrdinalIgnoreCase)
                && destination.Port == port)
            {
                return null;   // GetProxy echoes the target when nothing is configured
            }

            return new ProxyEndpoint(destination.Host, destination.Port);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Opens the CONNECT tunnel and leaves the stream positioned at the TLS record layer.</summary>
    public static async Task<bool> EstablishAsync(
        Stream stream,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var request = Encoding.ASCII.GetBytes(
            $"CONNECT {host}:{port} HTTP/1.1\r\nHost: {host}:{port}\r\nProxy-Connection: keep-alive\r\n\r\n");

        try
        {
            await stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            return false;
        }

        // Read headers only: the tunnel starts immediately after the blank line.
        var response = new StringBuilder();
        var buffer = new byte[256];
        while (!response.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
            {
                return false;
            }

            if (read == 0)
            {
                return false;
            }

            response.Append(Encoding.ASCII.GetString(buffer, 0, read));

            if (response.Length > 4096)
            {
                return false;   // a rogue proxy flooding us with headers
            }
        }

        var statusLine = response.ToString().Split('\n')[0].TrimEnd('\r');
        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[1] == "200";
    }
}
