using System.Net;
using AirNet.Core.Probes;
using Xunit;

namespace AirNet.Core.Tests;

public sealed class ProxyTunnelTests
{
    [Fact]
    public void No_proxy_means_a_direct_connection()
    {
        Assert.Null(ProxyTunnel.Choose(null, "www.baidu.com", 443));
    }

    [Fact]
    public void A_bypassed_host_is_connected_directly()
    {
        var proxy = new FakeProxy("127.0.0.1", 7897, bypassed: true);

        Assert.Null(ProxyTunnel.Choose(proxy, "www.baidu.com", 443));
    }

    [Fact]
    public void A_proxy_echoing_the_target_is_not_a_proxy()
    {
        // DefaultProxy returns the original URI when nothing is configured; treating that as a
        // proxy would send every request through a tunnel to the site itself.
        var proxy = new EchoProxy();

        Assert.Null(ProxyTunnel.Choose(proxy, "www.baidu.com", 443));
    }

    [Fact]
    public void A_configured_proxy_is_returned_with_its_endpoint()
    {
        var proxy = new FakeProxy("127.0.0.1", 7897, bypassed: false);

        var endpoint = ProxyTunnel.Choose(proxy, "www.baidu.com", 443);

        Assert.NotNull(endpoint);
        Assert.Equal("127.0.0.1", endpoint!.Host);
        Assert.Equal(7897, endpoint.Port);
    }

    [Fact]
    public void A_malformed_hostname_does_not_throw()
    {
        var proxy = new FakeProxy("127.0.0.1", 7897, bypassed: false);

        Assert.Null(ProxyTunnel.Choose(proxy, "not a host", 443));
    }

    private sealed class FakeProxy(string host, int port, bool bypassed) : IWebProxy
    {
        public ICredentials? Credentials { get; set; } = CredentialCache.DefaultCredentials;

        public Uri? GetProxy(Uri destination) => new($"http://{host}:{port}/");

        public bool IsBypassed(Uri host) => bypassed;
    }

    private sealed class EchoProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; } = CredentialCache.DefaultCredentials;

        public Uri? GetProxy(Uri destination) => destination;

        public bool IsBypassed(Uri host) => false;
    }
}
