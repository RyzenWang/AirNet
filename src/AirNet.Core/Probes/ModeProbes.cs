using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using AirNet.Core.Diagnostics;

namespace AirNet.Core.Probes;

public sealed record GameModeResult(
    bool UdpReachable,
    double RttMs,
    double JitterMs,
    double LossPercent,
    string NatSummary,
    string Target,
    DateTimeOffset At)
{
    public string Headline => UdpReachable ? "UDP 可达" : "UDP 无响应";
}

public sealed record MeetingModeResult(
    double UploadMbps,
    double LatencyMs,
    double JitterMs,
    double LossPercent,
    bool HasUpload,
    string Verdict,
    DateTimeOffset At);

/// <summary>
/// On-demand game/meeting checks. These are deliberately separate from the 3-second scan:
/// the game check sends a handful of STUN packets to a public endpoint, while the meeting check
/// measures upload plus TCP-handshake jitter. Both are read-only and can be cancelled.
/// </summary>
public static class GameModeProbe
{
    public const string DefaultTarget = "stun.l.google.com";
    public const int DefaultPort = 19302;

    public static async Task<GameModeResult> RunAsync(
        string target = DefaultTarget,
        int port = DefaultPort,
        int samples = 6,
        CancellationToken cancellationToken = default)
    {
        var at = DateTimeOffset.Now;
        var attempts = Math.Max(1, samples);
        IPAddress? server = null;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(target, cancellationToken).ConfigureAwait(false);
            server = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
                     ?? addresses.FirstOrDefault();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return new GameModeResult(false, 0, 0, 100, "DNS 解析失败", target, at);
        }

        if (server is null)
        {
            return new GameModeResult(false, 0, 0, 100, "无可用地址", target, at);
        }

        var rtts = new List<double>(attempts);
        var mappings = new List<IPEndPoint>(attempts);

        for (var i = 0; i < attempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await StunProbe.QueryAsync(server, port, timeoutMs: 1200, cancellationToken)
                .ConfigureAwait(false);
            if (result is not null)
            {
                rtts.Add(result.RttMs);
                mappings.Add(result.Mapped);
            }

            if (i < attempts - 1)
            {
                await Task.Delay(70, cancellationToken).ConfigureAwait(false);
            }
        }

        if (rtts.Count == 0)
        {
            return new GameModeResult(false, 0, 0, 100, "UDP 被拦截 / STUN 无响应", target, at);
        }

        var loss = (attempts - rtts.Count) * 100d / attempts;
        var jitter = rtts.Count < 2
            ? 0
            : rtts.Zip(rtts.Skip(1), (left, right) => Math.Abs(right - left)).Average();
        var mappedPorts = mappings.Select(item => item.Port).Distinct().Count();
        var mappedAddresses = mappings.Select(item => item.Address).Distinct().Count();
        var nat = mappedPorts == 1
            ? "端口保持（锥形 NAT 可能）"
            : "端口变化（对称 NAT 可能）";
        if (mappedAddresses > 1)
        {
            nat += " · 映射地址变化";
        }

        return new GameModeResult(
            true,
            Math.Round(rtts.Order().ElementAt(rtts.Count / 2), 1),
            Math.Round(jitter, 1),
            Math.Round(loss, 1),
            nat,
            target,
            at);
    }
}

public static class MeetingModeProbe
{
    private const string UploadUrl = "https://speed.cloudflare.com/__up";

    public static async Task<MeetingModeResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var at = DateTimeOffset.Now;
        var uploadTask = ThroughputProbe.MeasureUploadMbpsAsync(
            UploadUrl,
            bytes: 2 * 1024 * 1024,
            timeoutMs: 15000,
            cancellationToken);
        var latencySamples = await LatencyProbe.MeasureTcpSeriesAsync(
            "223.5.5.5",
            53,
            count: 8,
            intervalMs: 70,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var stats = LatencyAnalyzer.Analyze(latencySamples);
        var upload = await uploadTask.ConfigureAwait(false);

        return new MeetingModeResult(
            Math.Round(upload, 1),
            Math.Round(stats.AverageMs, 1),
            Math.Round(stats.JitterMs, 1),
            Math.Round(stats.LossPercent, 1),
            upload > 0,
            MeetingVerdict(upload, stats.JitterMs, stats.LossPercent),
            at);
    }

    public static string MeetingVerdict(double uploadMbps, double jitterMs, double lossPercent)
    {
        if (uploadMbps <= 0)
        {
            return "上行未测出";
        }

        if (uploadMbps >= 5 && jitterMs <= 30 && lossPercent <= 1.5)
        {
            return "可开 1080p";
        }

        if (uploadMbps >= 2.5 && jitterMs <= 50 && lossPercent <= 3)
        {
            return "可开 720p / 语音";
        }

        return "会议体验风险高";
    }
}

/// <summary>A deliberately tiny STUN binding client, used only to test UDP reachability and mapping.</summary>
internal static class StunProbe
{
    private const ushort BindingRequest = 0x0001;
    private const ushort BindingResponse = 0x0101;
    private const uint MagicCookie = 0x2112A442;
    private const ushort XorMappedAddress = 0x0020;
    private const ushort MappedAddress = 0x0001;

    public sealed record Result(double RttMs, IPEndPoint Local, IPEndPoint Mapped);

    public static async Task<Result?> QueryAsync(
        IPAddress server,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var transactionId = new byte[12];
        RandomNumberGenerator.Fill(transactionId);
        var request = BuildRequest(transactionId);

        try
        {
            using var socket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(server, port));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);

            var buffer = new byte[512];
            var watch = Stopwatch.StartNew();
            await socket.SendAsync(request, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            watch.Stop();

            var mapped = ParseMapped(buffer.AsSpan(0, received), transactionId);
            if (mapped is null)
            {
                return null;
            }

            return new Result(
                watch.Elapsed.TotalMilliseconds,
                socket.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0),
                mapped);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    public static byte[] BuildRequest(ReadOnlySpan<byte> transactionId)
    {
        var request = new byte[20];
        BinaryPrimitives.WriteUInt16BigEndian(request, BindingRequest);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(4), MagicCookie);
        transactionId[..12].CopyTo(request.AsSpan(8));
        return request;
    }

    /// <summary>Reads XOR-MAPPED-ADDRESS/MAPPED-ADDRESS; returns null for a malformed or unrelated packet.</summary>
    internal static IPEndPoint? ParseMapped(ReadOnlySpan<byte> response, ReadOnlySpan<byte> transactionId)
    {
        if (response.Length < 20
            || BinaryPrimitives.ReadUInt16BigEndian(response) != BindingResponse
            || BinaryPrimitives.ReadUInt32BigEndian(response[4..]) != MagicCookie
            || !response.Slice(8, 12).SequenceEqual(transactionId))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(response[2..]);
        var end = Math.Min(response.Length, 20 + length);
        var offset = 20;
        while (offset + 4 <= end)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(response[offset..]);
            var attributeLength = BinaryPrimitives.ReadUInt16BigEndian(response[(offset + 2)..]);
            var valueOffset = offset + 4;
            if (valueOffset + attributeLength > end)
            {
                return null;
            }

            if (type is XorMappedAddress or MappedAddress && attributeLength >= 8)
            {
                var family = response[valueOffset + 1];
                if (family != 0x01)
                {
                    return null;   // this client only creates IPv4 sockets
                }

                var portValue = BinaryPrimitives.ReadUInt16BigEndian(response[(valueOffset + 2)..]);
                var port = type == XorMappedAddress
                    ? portValue ^ 0x2112
                    : portValue;
                var addressBytes = response.Slice(valueOffset + 4, 4).ToArray();
                if (type == XorMappedAddress)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        addressBytes[i] ^= (byte)(MagicCookie >> (24 - (i * 8)));
                    }
                }

                return new IPEndPoint(new IPAddress(addressBytes), port);
            }

            offset = valueOffset + ((attributeLength + 3) & ~3);
        }

        return null;
    }
}
