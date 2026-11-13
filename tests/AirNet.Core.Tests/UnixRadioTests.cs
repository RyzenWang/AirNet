using AirNet.Core.Probes;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class UnixRadioTests
{
    private const string MacAssociated = """
        Wi-Fi:

              Interfaces:
                en0:
                  Status: Connected

              Current Network Information:
                AirNet-5G:
                  PHY Mode: 802.11ax
                  Channel: 149 (5GHz, 80MHz)
                  Country Code: CN
                  Security: WPA3 Personal
                  Signal / Noise: -52 dBm / -92 dBm
                  Transmit Rate: 1200

              Other Local Wi-Fi Networks:
                Neighbour:
                  PHY Mode: 802.11ac
        """;

    private const string MacDisconnected = """
        Wi-Fi:

              Interfaces:
                en0:
                  Status: Off

              Other Local Wi-Fi Networks:
                Neighbour:
                  PHY Mode: 802.11ac
        """;

    private const string LinuxIw = """
        Connected to aa:bb:cc:dd:ee:ff (on wlan0)
        	SSID: AirNet-5G
        	freq: 5745.0
        	signal: -47 dBm
        	tx bitrate: 1200.9 MBit/s
        """;

    private const string LinuxProc = """
        Inter-| sta-|   Quality        |   Discarded packets               | Missed | WE
         face | tus | link level noise |  nwid  crypt   frag  retry   misc | beacon | 22
         wlan0: 0000   57.  -53.  -89.       0      0      0      0      0        0
        """;

    [Fact]
    public void Mac_parsing_reads_ssid_phy_signal_and_rate()
    {
        var state = UnixRadio.ParseMacSystemProfiler(MacAssociated);

        Assert.NotNull(state);
        Assert.Equal("AirNet-5G", state!.Ssid);
        Assert.Equal("Wi-Fi 6", state.PhyLabel);
        Assert.Equal(96, state.SignalPercent);          // 2 × (-52 + 100)
        Assert.Equal(1_200_000, state.RxRateKbps);      // 1200 Mbps
    }

    [Fact]
    public void Mac_without_an_association_returns_nothing()
    {
        Assert.Null(UnixRadio.ParseMacSystemProfiler(MacDisconnected));
    }

    [Fact]
    public void Linux_parsing_reads_iw_output()
    {
        var state = UnixRadio.ParseLinuxIwLink(LinuxIw, LinuxProc, sysSpeedMbps: null);

        Assert.NotNull(state);
        Assert.Equal("AirNet-5G", state!.Ssid);
        Assert.Equal(100, state.SignalPercent);         // 2 × (-47 + 100) clamps to 100
        Assert.Equal(1_201_000, state.RxRateKbps);
    }

    [Fact]
    public void Linux_falls_back_to_proc_net_wireless_when_iw_has_no_signal()
    {
        var state = UnixRadio.ParseLinuxIwLink(string.Empty, LinuxProc, sysSpeedMbps: 866);

        Assert.NotNull(state);
        Assert.Equal(94, state!.SignalPercent);         // -53 dBm
        Assert.Equal(866_000, state.RxRateKbps);        // from /sys/class/net/<if>/speed
    }

    [Fact]
    public void Linux_with_nothing_readable_returns_nothing()
    {
        Assert.Null(UnixRadio.ParseLinuxIwLink(string.Empty, null, null));
    }

    [Theory]
    [InlineData(-30, 100)]
    [InlineData(-50, 100)]
    [InlineData(-70, 60)]
    [InlineData(-90, 20)]
    [InlineData(-120, 0)]
    public void Signal_strength_maps_to_a_clamped_percentage(int dbm, int expected)
    {
        Assert.Equal(expected, UnixRadio.PercentFromDbm(dbm));
    }

    [Theory]
    [InlineData("802.11be", "Wi-Fi 7")]
    [InlineData("802.11ax", "Wi-Fi 6")]
    [InlineData("802.11ac", "Wi-Fi 5")]
    [InlineData("802.11n", "Wi-Fi 4")]
    [InlineData("802.11g", "旧标准")]
    [InlineData("", "")]
    public void Phy_modes_are_translated_to_generations(string mode, string expected)
    {
        Assert.Equal(expected, UnixRadio.PhyFromMacMode(mode));
    }

    [Fact]
    public void Proc_net_wireless_ignores_a_quality_style_level()
    {
        // Some drivers report 57/70 instead of dBm; that must not become a fake -57 dBm.
        const string qualityStyle = """
            Inter-| sta-|   Quality        |   Discarded packets
             face | tus | link level noise |
             wlan0: 0000   57.   57.  -89.
            """;

        Assert.Null(UnixRadio.SignalFromProcNetWireless(qualityStyle));
    }
}
