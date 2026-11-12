using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AirNet.Core.Probes;

/// <summary>
/// A minimal DNS client: build one A query, send it over UDP, time the answer.
/// </summary>
/// <remarks>
/// Deliberately not a resolver. The question is "how long does this server take to answer",
/// which needs a real packet and nothing else — pulling in a DNS library for that would be a
/// dependency for one number.
/// </remarks>
public static class DnsProbe
{
    public const int DnsPort = 53;

    /// <summary>Median answer time in ms, or null when the server did not answer in time.</summary>
    public static async Task<double?> QueryAsync(
        IPAddress server,
        string host,
        int timeoutMs = 300,
        CancellationToken cancellationToken = default)
    {
        var samples = new List<double?>(3);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await QueryOnceAsync(server, host, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                samples.Add(result);
            }

            if (attempt < 2)
            {
                await Task.Delay(30, cancellationToken).ConfigureAwait(false);
            }
        }

        if (samples.Count == 0)
        {
            return null;
        }

        samples.Sort();
        return Math.Round(samples[samples.Count / 2]!.Value, 1);
    }

    /// <summary>One query. Returns null on timeout, refusal or a malformed answer.</summary>
    public static async Task<double?> QueryOnceAsync(
        IPAddress server,
        string host,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        var query = BuildQuery(host, id);

        try
        {
            using var socket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(server, DnsPort));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);

            var buffer = new byte[512];
            var stopwatch = Stopwatch.StartNew();
            await socket.SendAsync(query, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();

            return LooksLikeAnswer(buffer.AsSpan(0, received), id) ? stopwatch.Elapsed.TotalMilliseconds : null;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Encodes a single A/IN question; kept public so the wire format can be unit-tested.</summary>
    public static byte[] BuildQuery(string host, ushort id)
    {
        var labels = host.TrimEnd('.').Split('.');
        var length = 12 + 5 + labels.Sum(label => label.Length + 1);
        var buffer = new byte[length];

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), id);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), 0x0100);   // recursion desired
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4), 1);        // one question

        var offset = 12;
        foreach (var label in labels)
        {
            buffer[offset++] = (byte)label.Length;
            offset += Encoding.ASCII.GetBytes(label, buffer.AsSpan(offset));
        }

        buffer[offset++] = 0;                                              // root label
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 1);    // QTYPE  A
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset + 2), 1);// QCLASS IN
        return buffer;
    }

    /// <summary>A usable answer: same id, response bit set, no error code, at least one record.</summary>
    public static bool LooksLikeAnswer(ReadOnlySpan<byte> data, ushort id)    {
        if (data.Length < 12)
        {
            return false;
        }

        if (BinaryPrimitives.ReadUInt16BigEndian(data) != id)
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var isResponse = (flags & 0x8000) != 0;
        var rcode = flags & 0x000F;
        var answers = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);

        return isResponse && rcode == 0 && answers > 0;
    }

    /// <summary>The two public resolvers the app offers, timed against the same host.</summary>
    public static async Task<IReadOnlyList<Diagnostics.DnsServerTiming>> MeasurePublicAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        var targets = new (string Label, string Address)[]
        {
            ("阿里 DNS", "223.5.5.5"),
            ("Cloudflare", "1.1.1.1"),
        };

        var tasks = targets.Select(async target =>
        {
            if (!IPAddress.TryParse(target.Address, out var address))
            {
                return new Diagnostics.DnsServerTiming(target.Label, target.Address, null, 0);
            }

            var median = await QueryAsync(address, host, timeoutMs: 300, cancellationToken).ConfigureAwait(false);
            return new Diagnostics.DnsServerTiming(target.Label, target.Address, median, median is null ? 0 : 3);
        });

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
