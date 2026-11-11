using System.Net.NetworkInformation;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

/// <summary>
/// Finds the path MTU by sending don't-fragment echoes of decreasing size. No elevation and no
/// raw sockets: <see cref="PingOptions.DontFragment"/> is enough, and the failure mode itself
/// (PacketTooBig) is the measurement.
/// </summary>
public static class MtuProbe
{
    private const int Ipv4Overhead = 28;   // 20 byte header + 8 byte ICMP

    public static async Task<MtuResult> MeasureAsync(
        string host,
        int floor = 1200,
        int ceiling = 1472,
        CancellationToken cancellationToken = default)
    {
        using var ping = new Ping();
        var options = new PingOptions(64, dontFragment: true);

        var low = floor;
        var high = ceiling;
        var best = 0;
        var attempted = 0;

        while (low <= high && attempted < 8)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempted++;

            var probe = (low + high) / 2;
            if (await EchoAsync(ping, host, probe, options).ConfigureAwait(false))
            {
                best = probe;
                low = probe + 1;
            }
            else
            {
                high = probe - 1;
            }
        }

        if (best == 0)
        {
            return MtuResult.Unavailable("目标不响应 DF 探测（可能被防火墙拦截）");
        }

        return new MtuResult(
            best + Ipv4Overhead,
            best,
            true,
            best >= 1472 ? "标准以太网 MTU，无分片" : "小于 1500，存在隧道或 PPPoE 开销");
    }

    private static async Task<bool> EchoAsync(
        Ping ping,
        string host,
        int payloadBytes,
        PingOptions options)
    {
        try
        {
            var reply = await ping.SendPingAsync(host, 1200, new byte[payloadBytes], options)
                .ConfigureAwait(false);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception ex) when (ex is PingException or System.Net.Sockets.SocketException
                                       or InvalidOperationException)
        {
            return false;
        }
    }
}
