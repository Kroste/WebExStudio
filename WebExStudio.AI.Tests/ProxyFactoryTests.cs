using System.Net;
using WebExStudio.AI;
using Xunit;

namespace WebExStudio.AI.Tests;

public class ProxyFactoryTests
{
    [Fact]
    public void Create_ReturnsNull_WhenNoServer()
    {
        Assert.Null(ProxyFactory.Create(null));
        Assert.Null(ProxyFactory.Create("   "));
    }

    [Fact]
    public void Create_BuildsProxy_WithServer()
    {
        var proxy = Assert.IsType<WebProxy>(ProxyFactory.Create("http://proxy:8080"));
        Assert.Equal("proxy", proxy.Address!.Host);
        Assert.Equal(8080, proxy.Address.Port);
    }

    [Fact]
    public void Create_SetsCredentials_AndBypass()
    {
        var proxy = (WebProxy)ProxyFactory.Create(
            "http://proxy:8080", "localhost, 127.0.0.1", "max", "geheim")!;

        var cred = Assert.IsType<NetworkCredential>(proxy.Credentials);
        Assert.Equal("max", cred.UserName);
        Assert.Equal("geheim", cred.Password);
        Assert.Equal(["localhost", "127.0.0.1"], proxy.BypassList);
    }

    [Fact]
    public void CreateHttpClient_AppliesTimeout()
    {
        using var http = ProxyFactory.CreateHttpClient(
            "http://proxy:8080", null, null, null, TimeSpan.FromSeconds(42));
        Assert.Equal(TimeSpan.FromSeconds(42), http.Timeout);
    }
}
