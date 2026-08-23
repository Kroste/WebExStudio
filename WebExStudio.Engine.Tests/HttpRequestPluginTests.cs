using System.Net;
using System.Net.Sockets;
using WebExStudio.Core.Models;
using WebExStudio.Engine.Plugins;
using Xunit;
using EngineContext = WebExStudio.Engine.ExecutionContext;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// End-to-End-Test des HTTP-Plugins: die gebaute HttpRequestPlugin.dll wird über den echten
/// Plugin-Loader (AssemblyLoadContext) geladen und der Handler gegen einen lokalen
/// Loopback-Server (HttpListener) ausgeführt — keine echten Netzwerk-Aufrufe.
/// </summary>
public sealed class HttpRequestPluginTests : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _baseUrl;
    private string? _lastBody;
    private string? _lastContentType;
    private string? _lastMethod;

    public HttpRequestPluginTests()
    {
        var port = FreePort();
        _baseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; } // Listener gestoppt

            _lastMethod = ctx.Request.HttpMethod;
            _lastContentType = ctx.Request.ContentType;
            using (var reader = new StreamReader(ctx.Request.InputStream))
                _lastBody = await reader.ReadToEndAsync();

            var (status, body) = ctx.Request.Url!.AbsolutePath switch
            {
                "/ok" => (200, "pong"),
                "/echo" => (200, _lastBody ?? ""),
                "/fail" => (500, "boom"),
                _ => (404, "nope"),
            };

            ctx.Response.StatusCode = status;
            var buf = System.Text.Encoding.UTF8.GetBytes(body);
            await ctx.Response.OutputStream.WriteAsync(buf);
            ctx.Response.Close();
        }
    }

    private static NodePluginNode LoadNode()
    {
        var src = typeof(HttpRequestPlugin.HttpRequestPlugin).Assembly.Location;
        var dir = Directory.CreateTempSubdirectory("webex_http_").FullName;
        File.Copy(src, Path.Combine(dir, Path.GetFileName(src)));
        var deps = Path.ChangeExtension(src, ".deps.json");
        if (File.Exists(deps)) File.Copy(deps, Path.Combine(dir, Path.GetFileName(deps)));
        return Assert.Single(NodePluginLoader.LoadFromDirectory(dir), n => n.Definition.Type == "http_request");
    }

    private static EngineContext NewContext() =>
        new(page: null!, new TargetConfig { Name = "t" }, new RunConfig(), projectDir: "");

    [Fact]
    public void LoadsViaAssemblyLoadContext_AndExposesNode()
    {
        var node = LoadNode();
        Assert.Equal("HTTP-Anfrage", node.Definition.DisplayName);
        Assert.Equal(1, node.Definition.OutputPorts);
        Assert.Contains(node.Definition.Properties, p => p.Key == "url");
    }

    [Fact]
    public async Task Get_WritesBodyAndStatusToPayload()
    {
        var node = LoadNode();
        var ctx = NewContext();

        await node.Handler.ExecuteAsync(ctx, new FlowNode
        {
            Type = "http_request",
            Config = new() { ["url"] = _baseUrl + "ok", ["method"] = "GET" },
        });

        Assert.Equal("pong", ctx.Get("response"));
        Assert.Equal("200", ctx.Get("response_status"));
        Assert.Equal("GET", _lastMethod);
    }

    [Fact]
    public async Task Post_SendsBodyAndContentTypeHeader()
    {
        var node = LoadNode();
        var ctx = NewContext();

        await node.Handler.ExecuteAsync(ctx, new FlowNode
        {
            Type = "http_request",
            Config = new()
            {
                ["url"] = _baseUrl + "echo",
                ["method"] = "POST",
                ["headers"] = "Content-Type: application/json",
                ["body"] = "{\"text\":\"ping\"}",
                ["ctx_key"] = "antwort",
            },
        });

        Assert.Equal("POST", _lastMethod);
        Assert.Equal("{\"text\":\"ping\"}", _lastBody);
        Assert.StartsWith("application/json", _lastContentType);
        Assert.Equal("{\"text\":\"ping\"}", ctx.Get("antwort")); // /echo spiegelt den Body
    }

    [Fact]
    public async Task FailOnError_ThrowsOnServerError()
    {
        var node = LoadNode();
        var ctx = NewContext();

        await Assert.ThrowsAnyAsync<Exception>(() => node.Handler.ExecuteAsync(ctx, new FlowNode
        {
            Type = "http_request",
            Config = new() { ["url"] = _baseUrl + "fail", ["fail_on_error"] = "true" },
        }));

        Assert.Equal("500", ctx.Get("response_status")); // Status wird trotzdem gesetzt
    }

    public void Dispose()
    {
        if (_listener.IsListening) _listener.Stop();
        ((IDisposable)_listener).Dispose();
    }
}
