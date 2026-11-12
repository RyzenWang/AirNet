using AirNet.Core.Diagnostics;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class InterferenceTests
{
    [Fact]
    public void Nothing_in_the_way_reports_nothing()
    {
        var result = InterferenceAnalyzer.Evaluate(false, null, []);

        Assert.False(result.IsInterfering);
        Assert.Equal(string.Empty, result.ChipText);
        Assert.Equal(string.Empty, result.Note);
    }

    [Fact]
    public void A_proxy_is_reported_as_partial_interference()
    {
        var result = InterferenceAnalyzer.Evaluate(true, "127.0.0.1:7890", null);

        Assert.True(result.ProxyConfigured);
        Assert.False(result.TunnelPresent);
        Assert.Equal("经代理", result.ChipText);
        Assert.Contains("127.0.0.1:7890", result.ProxyText);

        // The important nuance: a proxy only touches the HTTP probes.
        Assert.Contains("ICMP", result.Note);
    }

    [Fact]
    public void A_tunnel_is_reported_as_full_interference()
    {
        var result = InterferenceAnalyzer.Evaluate(false, null, ["WireGuard Tunnel"]);

        Assert.True(result.TunnelPresent);
        Assert.Equal("经 VPN", result.ChipText);
        Assert.Contains("WireGuard Tunnel", result.TunnelText);
        Assert.Contains("隧道", result.Note);
    }

    [Fact]
    public void Tunnel_wins_the_label_when_both_are_present()
    {
        var result = InterferenceAnalyzer.Evaluate(true, "10.0.0.1:8080", ["OpenVPN"]);

        Assert.Equal("经 VPN", result.ChipText);
        Assert.Contains("VPN", result.Note);
        Assert.Contains("代理", result.Note);
    }

    [Fact]
    public void Several_tunnels_are_summarised_not_listed()
    {
        var result = InterferenceAnalyzer.Evaluate(false, null, ["WireGuard", "Tailscale", "ZeroTier"]);

        Assert.Contains("3", result.TunnelText);
        Assert.Contains("WireGuard", result.TunnelText);
    }

    [Fact]
    public void Blank_adapter_names_are_ignored()
    {
        var result = InterferenceAnalyzer.Evaluate(false, null, ["", "  "]);

        Assert.False(result.TunnelPresent);
        Assert.False(result.IsInterfering);
    }

    [Fact]
    public void Proxy_without_a_known_address_still_counts()
    {
        var result = InterferenceAnalyzer.Evaluate(true, null, []);

        Assert.True(result.ProxyConfigured);
        Assert.Contains("已启用", result.ProxyText);
    }
}
