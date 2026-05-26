using System.Text.Json.Serialization;

namespace WebExStudio.Core.Models;

public sealed class RunConfig
{
    [JsonPropertyName("browser")]
    public string Browser { get; set; } = "chromium";

    /// <summary>System-browser channel (e.g. "chrome", "msedge"). Empty = use bundled Chromium/Firefox.</summary>
    [JsonPropertyName("browser_channel")]
    public string BrowserChannel { get; set; } = string.Empty;

    /// <summary>Path to the browser executable. Empty = let Playwright pick (system/bundled default).</summary>
    [JsonPropertyName("browser_executable_path")]
    public string BrowserExecutablePath { get; set; } = string.Empty;

    /// <summary>Path to the Playwright driver folder, used when it can't be located automatically.</summary>
    [JsonPropertyName("driver_path")]
    public string DriverPath { get; set; } = string.Empty;

    [JsonPropertyName("headless")]
    public bool Headless { get; set; } = false;

    /// <summary>Startet das (sichtbare) Browserfenster maximiert. Nur bei Chromium-basierten
    /// Browsern (Chromium/Chrome/Edge/Brave) und nicht im Headless-Modus wirksam.</summary>
    [JsonPropertyName("maximized")]
    public bool Maximized { get; set; } = false;

    [JsonPropertyName("slow_mo_ms")]
    public int SlowMoMs { get; set; } = 0;

    /// <summary>Sitzung (Cookies + localStorage) beim Start laden und so Login/Captcha
    /// wiederverwenden. Geschrieben wird die Sitzung über den <c>save_session</c>-Node.</summary>
    [JsonPropertyName("session_persist")]
    public bool SessionPersist { get; set; } = false;

    /// <summary>Pfad zur Sitzungsdatei (storageState-JSON). Leer = <c>session.json</c> im Projektordner.</summary>
    [JsonPropertyName("session_file")]
    public string SessionFile { get; set; } = string.Empty;

    [JsonPropertyName("timeout_ms")]
    public int TimeoutMs { get; set; } = 30000;

    [JsonPropertyName("download_dir")]
    public string DownloadDir { get; set; } = string.Empty;

    /// <summary>Proxy-Server (z. B. "http://proxy:8080"). Leer = kein Proxy / Systemstandard.</summary>
    [JsonPropertyName("proxy_server")]
    public string ProxyServer { get; set; } = string.Empty;

    /// <summary>Hosts ohne Proxy, kommagetrennt (z. B. "localhost,127.0.0.1,*.intern").</summary>
    [JsonPropertyName("proxy_bypass")]
    public string ProxyBypass { get; set; } = string.Empty;

    [JsonPropertyName("proxy_username")]
    public string ProxyUsername { get; set; } = string.Empty;

    [JsonPropertyName("proxy_password")]
    public string ProxyPassword { get; set; } = string.Empty;

    [JsonPropertyName("project_dir")]
    public string ProjectDir { get; set; } = string.Empty;

    /// <summary>Global context variables merged with every target's ctx.</summary>
    [JsonPropertyName("ctx")]
    public Dictionary<string, string> Ctx { get; set; } = [];
}
