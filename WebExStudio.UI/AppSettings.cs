using System.Text.Json;
using NLog;
using WebExStudio.AI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI;

/// <summary>
/// Persists user settings (browser + driver configuration) to a JSON file in the
/// user's config directory, and applies them to a RunConfig.
/// </summary>
public static class AppSettings
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WebExStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    private sealed class Model
    {
        public string Browser { get; set; } = "chromium";
        public string BrowserChannel { get; set; } = "";
        public string BrowserExecutablePath { get; set; } = "";
        public string DriverPath { get; set; } = "";
        public bool Headless { get; set; }
        public string DownloadDir { get; set; } = "";

        // Netzwerk / Proxy
        public string ProxyServer { get; set; } = "";
        public string ProxyBypass { get; set; } = "";
        public string ProxyUsername { get; set; } = "";
        public string ProxyPassword { get; set; } = "";

        // KI-Anbindung
        public string AiProvider { get; set; } = "anthropic";
        public string AiApiKey { get; set; } = "";
        public string AiModel { get; set; } = "";
        public string AiBaseUrl { get; set; } = "";
    }

    private static Model ReadModel()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<Model>(File.ReadAllText(SettingsPath), Options) ?? new Model();
        }
        catch (Exception ex)
        {
            Log.Warn("Einstellungen konnten nicht gelesen werden: {0}", ex.Message);
        }
        return new Model();
    }

    private static void WriteModel(Model m)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(m, Options));
            Log.Info("Einstellungen gespeichert: {0}", SettingsPath);
        }
        catch (Exception ex)
        {
            Log.Warn("Einstellungen konnten nicht gespeichert werden: {0}", ex.Message);
        }
    }

    public static void Load(RunConfig config)
    {
        var m = ReadModel();
        config.Browser = m.Browser;
        config.BrowserChannel = m.BrowserChannel;
        config.BrowserExecutablePath = m.BrowserExecutablePath;
        config.DriverPath = m.DriverPath;
        config.Headless = m.Headless;
        config.DownloadDir = m.DownloadDir;
        config.ProxyServer = m.ProxyServer;
        config.ProxyBypass = m.ProxyBypass;
        config.ProxyUsername = m.ProxyUsername;
        config.ProxyPassword = m.ProxyPassword;
    }

    public static void Save(RunConfig config)
    {
        // Read-modify-write, damit der KI-Abschnitt erhalten bleibt.
        var m = ReadModel();
        m.Browser = config.Browser;
        m.BrowserChannel = config.BrowserChannel;
        m.BrowserExecutablePath = config.BrowserExecutablePath;
        m.DriverPath = config.DriverPath;
        m.Headless = config.Headless;
        m.DownloadDir = config.DownloadDir;
        m.ProxyServer = config.ProxyServer;
        m.ProxyBypass = config.ProxyBypass;
        m.ProxyUsername = config.ProxyUsername;
        m.ProxyPassword = config.ProxyPassword;
        WriteModel(m);
    }

    public static void LoadAi(AiOptions ai)
    {
        var m = ReadModel();
        ai.Provider = m.AiProvider;
        ai.ApiKey = m.AiApiKey;
        ai.Model = m.AiModel;
        ai.BaseUrl = m.AiBaseUrl;
    }

    public static void SaveAi(AiOptions ai)
    {
        // Read-modify-write, damit der Browser-Abschnitt erhalten bleibt.
        var m = ReadModel();
        m.AiProvider = ai.Provider;
        m.AiApiKey = ai.ApiKey;
        m.AiModel = ai.Model;
        m.AiBaseUrl = ai.BaseUrl;
        WriteModel(m);
    }
}
