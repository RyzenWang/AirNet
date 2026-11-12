using System.Buffers.Binary;
using AirNet.Core.Probes;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class DnsProbeTests
{
    [Fact]
    public void A_query_encodes_labels_and_one_A_record_question()
    {
        var query = DnsProbe.BuildQuery("www.baidu.com", 0x1234);

        Assert.Equal(0x12, query[0]);
        Assert.Equal(0x34, query[1]);
        Assert.Equal(0x01, query[2]);            // recursion desired
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(4)));

        // www(3) baidu(5) com(3) + three length bytes + root
        Assert.Equal(3, query[12]);
        Assert.Equal((byte)'w', query[13]);

        var tail = query.Length - 4;
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(tail)));      // QTYPE A
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(tail + 2)));  // QCLASS IN
        Assert.Equal(0, query[tail - 1]);                                               // root label
    }

    [Fact]
    public void Trailing_dots_do_not_add_an_empty_label()
    {
        var withDot = DnsProbe.BuildQuery("example.com.", 1);
        var without = DnsProbe.BuildQuery("example.com", 1);

        Assert.Equal(without.Length, withDot.Length);
    }

    [Theory]
    [InlineData(0x8000, 0, 1, true)]     // response, no error, one answer
    [InlineData(0x0000, 0, 1, false)]    // not a response
    [InlineData(0x8003, 3, 0, false)]    // NXDOMAIN
    [InlineData(0x8000, 0, 0, false)]    // no answer records
    public void Answer_validation_checks_flags_and_counts(int flags, int rcode, int answers, bool expected)
    {
        var response = new byte[16];
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0), 0xABCD);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2), (ushort)(flags | rcode));
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(6), (ushort)answers);

        Assert.Equal(expected, DnsProbe.LooksLikeAnswer(response, 0xABCD));
    }

    [Fact]
    public void A_response_with_a_foreign_id_is_rejected()
    {
        var response = new byte[16];
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0), 0x1111);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2), 0x8000);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(6), 1);

        Assert.False(DnsProbe.LooksLikeAnswer(response, 0x2222));
    }

    [Fact]
    public void A_truncated_packet_is_rejected_rather_than_throwing()
    {
        Assert.False(DnsProbe.LooksLikeAnswer([0x12, 0x34, 0x80], 0x1234));
    }
}
