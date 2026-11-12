using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Looks for a proxy or a VPN tunnel in front of the measurements.
/// </summary>
/// <remarks>
/// The proxy is read through <see cref="HttpClient.DefaultProxy"/>, which already resolves
/// WinINET settings on Windows and the <c>http(s)_proxy</c> variables elsewhere — no registry
/// poking and no per-platform code. Tunnels are found by adapter type and by name, because most
/// VPN clients present themselves as ordinary ethernet adapters.
/// </remarks>
public static class InterferenceProbe
{
    private static readonly string[] TunnelMarkers =
    [
        "wireguard", "openvpn", "tap-windows", "wintun", "tailscale", "zerotier",
        "nordlynx", "anyconnect", "globalprotect", "fortinet", "vpn", "tun", "pptp", "l2tp",
    ];

    public static InterferenceInfo Detect()
    {
        var (proxyConfigured, proxyAddress) = ReadProxy();
        var tunnels = ReadTunnels();

        return InterferenceAnalyzer.Evaluate(proxyConfigured, proxyAddress, tunnels);
    }

    private static (bool Configured, string Address) ReadProxy()
    {
        try
        {
            var probe = new Uri("https://www.example.com/");
            var proxy = HttpClient.DefaultProxy;
            var effective = proxy.GetProxy(probe);

            // DefaultProxy returns the original URI when nothing is configured.
            if (effective is not null
                && !string.Equals(effective.Host, probe.Host, StringComparison.OrdinalIgnoreCase))
            {
                return (true, $"{effective.Host}:{effective.Port}");
            }
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or InvalidOperationException or UriFormatException)
        {
            // A broken proxy configuration should not fail the scan.
        }

        // GetProxy() does not evaluate PAC scripts; an explicit variable is still worth reporting.
        var env = Environment.GetEnvironmentVariable("HTTPS_PROXY")
                  ?? Environment.GetEnvironmentVariable("https_proxy")
                  ?? Environment.GetEnvironmentVariable("ALL_PROXY");

        return string.IsNullOrWhiteSpace(env) ? (false, string.Empty) : (true, env);
    }

    private static IReadOnlyList<string> ReadTunnels()
    {
        var tunnels = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    tunnels.Add(nic.Name);
                    continue;
                }

                var haystack = $"{nic.Name} {nic.Description}".ToLowerInvariant();
                if (TunnelMarkers.Any(marker => haystack.Contains(marker, StringComparison.Ordinal)))
                {
                    tunnels.Add(nic.Name);
                }
            }
        }
        catch (NetworkInformationException)
        {
            return [];
        }

        return tunnels.Distinct().ToList();
    }
}
