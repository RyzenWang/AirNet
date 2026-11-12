namespace AirNet.Core.Diagnostics;

/// <summary>
/// Something standing between the app and the internet. This matters because the two kinds
/// interfere differently: an HTTP proxy only affects the HTTP(S) probes (bandwidth, egress,
/// site latency), while a VPN tunnel carries everything including raw TCP timing.
/// </summary>
public sealed record InterferenceInfo(
    bool ProxyConfigured,
    string ProxyText,
    bool TunnelPresent,
    string TunnelText)
{
    public static InterferenceInfo None { get; } = new(false, string.Empty, false, string.Empty);

    public bool IsInterfering => ProxyConfigured || TunnelPresent;

    /// <summary>Short label for a chip; empty when nothing is in the way.</summary>
    public string ChipText => TunnelPresent ? "经 VPN" : ProxyConfigured ? "经代理" : string.Empty;

    public string Note
    {
        get
        {
            if (TunnelPresent && ProxyConfigured)
            {
                return "测量经过 VPN 隧道，并叠加了系统代理：延迟、带宽、出口地址都不代表运营商线路。";
            }

            if (TunnelPresent)
            {
                return "测量经过 VPN 隧道：延迟与带宽描述的是隧道出口，不是本机到运营商的链路。";
            }

            return ProxyConfigured
                ? "HTTP 类测量（带宽、出口地址、首包分解与网站延迟）经过系统代理；ICMP 丢包与 TCP 握手计时仍走真实链路。"
                : string.Empty;
        }
    }
}

/// <summary>Pure detection results to verdict, so the wording and ranking can be tested.</summary>
public static class InterferenceAnalyzer
{
    public static InterferenceInfo Evaluate(
        bool proxyConfigured,
        string? proxyAddress,
        IReadOnlyList<string>? tunnelAdapters)
    {
        var tunnels = tunnelAdapters?.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray() ?? [];

        var proxyText = proxyConfigured
            ? string.IsNullOrWhiteSpace(proxyAddress) ? "系统代理已启用" : $"系统代理 {proxyAddress}"
            : string.Empty;

        var tunnelText = tunnels.Length switch
        {
            0 => string.Empty,
            1 => tunnels[0],
            _ => $"{tunnels[0]} 等 {tunnels.Length} 个隧道",
        };

        return new InterferenceInfo(proxyConfigured, proxyText, tunnels.Length > 0, tunnelText);
    }
}
