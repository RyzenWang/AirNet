using System.Net;
using System.Text.RegularExpressions;

namespace AirNet.Core.Probes;

/// <summary>
/// Reads the public egress address from a small plain-text endpoint. Two independent services are
/// queried in parallel because Cloudflare is blocked on some networks; whichever answers first
/// wins, and a failure is reported as "未获取" rather than blocking the scan.
/// </summary>
public static partial class EgressProbe
{
    private static readonly string[] Endpoints =
    [
        // The 3322 endpoint is a fast IPv4-only service reachable from mainland China; the
        // Cloudflare trace is the fallback for networks where that service is blocked. Both are
        // plain-text GETs with no query parameters.
        "https://ip.3322.net",
        "https://www.cloudflare.com/cdn-cgi/trace",
    ];

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.None,
        // The egress address is only meaningful when the request leaves directly; a corporate
        // proxy would report its own address. It also avoids .NET's slow system-proxy probe.
        UseProxy = false,
    })
    {
        Timeout = TimeSpan.FromMilliseconds(3000),
    };

    /// <summary>Returns null when every endpoint is blocked, slow or answers something unexpected.</summary>
    public static async Task<string?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(2500));

        var pending = Endpoints.Select(url => FetchAsync(url, deadline.Token)).ToList();
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(finished);

            var address = await finished.ConfigureAwait(false);
            if (address is not null)
            {
                return address;
            }
        }

        return null;
    }

    private static async Task<string?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var body = await Client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return Parse(body);
        }
        catch (Exception)
        {
            // Egress is a convenience chip, never a reason to fail a scan.
            return null;
        }
    }

    /// <summary>Accepts the Cloudflare key=value trace and the bare address returned by 3322.</summary>
    internal static string? Parse(string body)
    {
        var text = body.Trim();
        if (IPAddress.TryParse(text, out var direct))
        {
            return direct.ToString();
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (!trimmed.StartsWith("ip=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var address = trimmed[3..].Trim();
            if (IPAddress.TryParse(address, out var parsed))
            {
                return parsed.ToString();
            }
        }

        var match = Ipv4Regex().Match(text);
        return match.Success && IPAddress.TryParse(match.Value, out var found) ? found.ToString() : null;
    }

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b")]
    private static partial Regex Ipv4Regex();
}
