using System.Buffers.Binary;
using System.Net;
using AirNet.Core.Probes;

namespace AirNet.Core.Tests;

public sealed class ModeProbeTests
{
    [Fact]
    public void ParsesXorMappedAddress()
    {
        var transactionId = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        var response = BuildStunResponse(transactionId, xorMapped: true);

        var mapped = StunProbe.ParseMapped(response, transactionId);

        Assert.NotNull(mapped);
        Assert.Equal("203.0.113.42", mapped.Address.ToString());
        Assert.Equal(4660, mapped.Port);
    }

    [Fact]
    public void ParsesPlainMappedAddress()
    {
        var transactionId = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        var response = BuildStunResponse(transactionId, xorMapped: false);

        var mapped = StunProbe.ParseMapped(response, transactionId);

        Assert.NotNull(mapped);
        Assert.Equal("203.0.113.42", mapped.Address.ToString());
        Assert.Equal(4660, mapped.Port);
    }

    [Fact]
    public void RejectsWrongTransactionAndMalformedPackets()
    {
        var transactionId = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        var response = BuildStunResponse(transactionId, xorMapped: true);

        Assert.Null(StunProbe.ParseMapped(response, new byte[12]));
        Assert.Null(StunProbe.ParseMapped(response[..12], transactionId));
    }

    [Fact]
    public void BuildsBindingRequestWithMagicCookie()
    {
        var transactionId = Enumerable.Repeat((byte)0xAA, 12).ToArray();
        var request = StunProbe.BuildRequest(transactionId);

        Assert.Equal(20, request.Length);
        Assert.Equal(0x0001, BinaryPrimitives.ReadUInt16BigEndian(request));
        Assert.Equal(0x2112A442u, BinaryPrimitives.ReadUInt32BigEndian(request.AsSpan(4)));
        Assert.True(transactionId.AsSpan().SequenceEqual(request.AsSpan(8, 12)));
    }

    [Theory]
    [InlineData(8, 10, 0, "可开 1080p")]
    [InlineData(3, 20, 0, "可开 720p / 语音")]
    [InlineData(1, 10, 0, "会议体验风险高")]
    [InlineData(8, 10, 4, "会议体验风险高")]
    [InlineData(0, 0, 0, "上行未测出")]
    public void MeetingVerdictUsesUploadJitterAndLoss(double upload, double jitter, double loss, string expected)
    {
        Assert.Equal(expected, MeetingModeProbe.MeetingVerdict(upload, jitter, loss));
    }

    private static byte[] BuildStunResponse(byte[] transactionId, bool xorMapped)
    {
        var value = new byte[8];
        value[1] = 0x01;
        BinaryPrimitives.WriteUInt16BigEndian(value.AsSpan(2), xorMapped ? (ushort)(4660 ^ 0x2112) : (ushort)4660);
        new byte[] { 203, 0, 113, 42 }.CopyTo(value, 4);
        if (xorMapped)
        {
            for (var i = 0; i < 4; i++)
            {
                value[4 + i] ^= (byte)(0x2112A442 >> (24 - (i * 8)));
            }
        }

        var response = new byte[20 + 4 + value.Length];
        BinaryPrimitives.WriteUInt16BigEndian(response, 0x0101);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2), (ushort)(4 + value.Length));
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(4), 0x2112A442);
        transactionId.CopyTo(response, 8);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(20), xorMapped ? (ushort)0x0020 : (ushort)0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(22), (ushort)value.Length);
        value.CopyTo(response, 24);
        return response;
    }
}
