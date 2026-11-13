using AirNet.Core.Probes;

namespace AirNet.Core.Tests;

public sealed class WlanNeighborTests
{
    [Fact]
    public void ParsesBssEntryAndEstimatesRssi()
    {
        var buffer = new byte[72];
        BitConverter.GetBytes(7).CopyTo(buffer, 0);
        System.Text.Encoding.UTF8.GetBytes("Home-5G").CopyTo(buffer, 4);
        new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }.CopyTo(buffer, 40);
        BitConverter.GetBytes(10).CopyTo(buffer, 50);       // Wi-Fi 6
        BitConverter.GetBytes(80).CopyTo(buffer, 58);       // 80% -> -60 dBm
        BitConverter.GetBytes(1_200_000).CopyTo(buffer, 62);
        BitConverter.GetBytes(600_000).CopyTo(buffer, 66);

        var neighbor = WlanNeighborProbe.ParseEntry(buffer);

        Assert.NotNull(neighbor);
        Assert.Equal("Home-5G", neighbor.Ssid);
        Assert.Equal("AA:BB:CC:DD:EE:FF", neighbor.Bssid);
        Assert.Equal("Wi-Fi 6", neighbor.Phy);
        Assert.Equal(80, neighbor.SignalQuality);
        Assert.Equal(-60, neighbor.RssiDbm);
        Assert.Equal(1200, neighbor.RxMbps);
        Assert.Equal(600, neighbor.TxMbps);
    }

    [Fact]
    public void HiddenSsidIsIgnored()
    {
        var buffer = new byte[72];
        BitConverter.GetBytes(0).CopyTo(buffer, 0);

        Assert.Null(WlanNeighborProbe.ParseEntry(buffer));
    }

    [Fact]
    public void ShortEntryIsIgnored()
    {
        Assert.Null(WlanNeighborProbe.ParseEntry(new byte[12]));
    }
}
