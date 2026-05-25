using System.Net;

namespace WebExStudio.AI;

/// <summary>Erzeugt Proxy- und HttpClient-Instanzen aus den (Browser-/Netzwerk-)Einstellungen.</summary>
public static class ProxyFactory
{
    /// <summary>
    /// Baut einen <see cref="IWebProxy"/> oder gibt <c>null</c> zurück, wenn kein Server gesetzt ist.
    /// </summary>
    public static IWebProxy? Create(string? server, string? bypass = null, string? user = null, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(server)) return null;

        var proxy = new WebProxy(server.Trim());
        if (!string.IsNullOrWhiteSpace(user))
            proxy.Credentials = new NetworkCredential(user, password ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(bypass))
            proxy.BypassList = bypass
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return proxy;
    }

    /// <summary>Erzeugt einen <see cref="HttpClient"/>, der den Proxy nutzt (falls konfiguriert).</summary>
    public static HttpClient CreateHttpClient(
        string? server, string? bypass, string? user, string? password, TimeSpan timeout)
    {
        var proxy = Create(server, bypass, user, password);
        var handler = new HttpClientHandler();
        if (proxy is not null)
        {
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }
        return new HttpClient(handler) { Timeout = timeout };
    }
}
