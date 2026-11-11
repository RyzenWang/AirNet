using System.Net.NetworkInformation;
using System.Net.Sockets;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Reads the currently active interface through <see cref="NetworkInterface"/>. This needs no
/// elevation and no driver, which is what keeps AirNet portable. Radio signal strength is not
/// exposed by the BCL; the platform layer fills it in on Windows via wlanapi (see TECH_PLAN.md).
/// </summary>
public static class InterfaceInspector
{
    public static InterfaceInfo Inspect()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Nic: n, Props: n.GetIPProperties()))
            .Where(x => x.Props.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
            .Where(x => !IsVirtual(x.Nic))
            // A default gateway is the strongest evidence that this is the adapter actually
            // carrying traffic; Hyper-V, WSL and VPN switches usually have none.
            .OrderByDescending(x => x.Props.GatewayAddresses.Count > 0)
            .ThenByDescending(x => x.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1)
            .ThenByDescending(x => x.Nic.Speed)
            .ToList();

        if (candidates.Count == 0)
        {
            return InterfaceInfo.Unknown;
        }

        var (nic, props) = candidates[0];
        var ipv4 = props.UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "-";

        var gateway = props.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "-";

        var dns = props.DnsAddresses
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "-";

        var wireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
        return new InterfaceInfo(
            Name: nic.Name,
            Kind: wireless ? "Wi-Fi" : "有线以太网",
            IsWireless: wireless,
            SignalPercent: null,
            LinkSpeedMbps: nic.Speed > 0 ? Math.Round(nic.Speed / 1_000_000d, 0) : 0,
            LocalAddress: ipv4,
            Gateway: gateway,
            DnsServer: dns);
    }

    /// <summary>
    /// Virtual adapters (Hyper-V, WSL, containers, most VPN clients) are up and can carry
    /// traffic, but they are not what the user thinks of as their network. They are dropped
    /// unless nothing else is available.
    /// </summary>
    private static bool IsVirtual(NetworkInterface nic)
    {
        var description = $"{nic.Name} {nic.Description}".ToLowerInvariant();
        return nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
            or NetworkInterfaceType.Tunnel
            || description.Contains("vethernet")
            || description.Contains("hyper-v")
            || description.Contains("vmware")
            || description.Contains("virtualbox")
            || description.Contains("wsl")
            || description.Contains("loopback")
            || description.Contains("tap-windows")
            || description.Contains("wireguard")
            || description.Contains("openvpn");
    }
}
