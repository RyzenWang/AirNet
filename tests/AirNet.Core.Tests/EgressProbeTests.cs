using AirNet.Core.Probes;

namespace AirNet.Core.Tests;

public sealed class EgressProbeTests
{
    [Fact]
    public void ParsesTheCloudflareTraceFormat()
    {
        const string body = "fl=abc\nh=www.cloudflare.com\nip=203.0.113.42\nloc=CN\n";

        Assert.Equal("203.0.113.42", EgressProbe.Parse(body));
    }

    [Fact]
    public void ParsesABareAddress()
    {
        Assert.Equal("14.19.165.0", EgressProbe.Parse("14.19.165.0\r\n"));
    }

    [Fact]
    public void FallsBackToTheFirstIpv4InHumanReadableText()
    {
        Assert.Equal("1.2.3.4", EgressProbe.Parse("当前 IP：1.2.3.4  来自于：中国 上海 电信"));
    }

    [Fact]
    public void RejectsHtmlAndMalformedValues()
    {
        Assert.Null(EgressProbe.Parse("<html><body>blocked</body></html>"));
        Assert.Null(EgressProbe.Parse("ip=not-an-address"));
        Assert.Null(EgressProbe.Parse(string.Empty));
    }
}
