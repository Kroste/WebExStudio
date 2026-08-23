using System.Text.Json;
using NLog;
using WebExStudio.AI;
using WebExStudio.Core.Models;
using WebExStudio.Core.Security;
using WebExStudio.Core.Storage;

namespace WebExStudio.UI;

/// <summary>
/// Persists user settings (browser + driver configuration) to a JSON file in the
/// user's config directory, and applies them to a RunConfig.
/// </summary>
public static class AppSettings
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Verhindert die Rekursion beim einmaligen Nachziehen unverschlüsselter Altwerte.</summary>
    private static bool _migrating;

    private static string ConfigDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WebExStudio");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SettingsPath => Path.Combine(ConfigDir, "settings.json");

    /// <summary>Plugin-Ordner: neben der Anwendung und im Konfig-Ordner (beide werden durchsucht).</summary>
    public static string[] PluginDirs =>
    [
        Path.Combine(AppContext.BaseDirectory, "plugins"),
        Path.Combine(ConfigDir, "plugins"),
    ];

    private sealed class Model
    {
        public string Browser { get; set; } = "chromium";
        public string BrowserChannel { get; set; } = "";
        public string BrowserExecutablePath { get; set; } = "";
        public string DriverPath { get; set; } = "";
        public bool Headless { get; set; }
        public bool Maximized { get; set; }
        public bool SessionPersist { get; set; }
        public string SessionFile { get; set; } = "";
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
        public string AiHints { get; set; } = AiOptions.DefaultHints;
        public bool AiSendHints { get; set; } = true;

        // Oberfläche
        public bool SuggestionsEnabled { get; set; } = true;
        public List<string> RecentFiles { get; set; } = [];
        public string Language { get; set; } = "de";

        // Plugins (Dateinamen deaktivierter Plugins)
        public List<string> DisabledPlugins { get; set; } = [];

        /// <summary>
        /// Flache Kopie für die Ablage — damit das Verschlüsseln der Geheimfelder das
        /// Modell des Aufrufers nicht verändert. Die Listen werden bewusst kopiert,
        /// damit eine spätere Änderung an der Kopie nicht auf das Original durchschlägt.
        /// </summary>
        public Model CloneForDisk()
        {
            var c = (Model)MemberwiseClone();
            c.RecentFiles = [.. RecentFiles];
            c.DisabledPlugins = [.. DisabledPlugins];
            return c;
        }
    }

    /// <summary>
    /// Felder, die verschlüsselt auf Platte liegen. Im Speicher hält <see cref="Model"/> sie
    /// im Klartext — <see cref="ReadModel"/> entschlüsselt, <see cref="WriteModel"/> verschlüsselt.
    /// So kann keine Aufrufstelle das Verschlüsseln vergessen.
    /// </summary>
    /// <returns>True, wenn mindestens ein Wert noch als Klartext auf Platte lag.</returns>
    private static bool Decrypt(Model m)
    {
        var wasPlaintext = SecretProtection.IsPlaintext(m.ProxyPassword) || SecretProtection.IsPlaintext(m.AiApiKey);
        m.ProxyPassword = SecretProtection.UnprotectOrPlaintext(m.ProxyPassword);
        m.AiApiKey = SecretProtection.UnprotectOrPlaintext(m.AiApiKey);
        return wasPlaintext;
    }

    private static void Encrypt(Model m)
    {
        m.ProxyPassword = SecretProtection.Protect(m.ProxyPassword);
        m.AiApiKey = SecretProtection.Protect(m.AiApiKey);
    }

    private static Model ReadModel()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var m = JsonSerializer.Deserialize<Model>(File.ReadAllText(SettingsPath), Options) ?? new Model();
                if (Decrypt(m) && !_migrating)
                {
                    // Altbestand aus einer Version vor der Verschlüsselung: sofort nachziehen, statt
                    // darauf zu warten, dass der Nutzer irgendwann die Einstellungen speichert.
                    // Das Flag verhindert eine Endlosschleife über WriteModel → ReadModel.
                    Log.Info("Geheimwerte lagen im Klartext vor — werden jetzt verschlüsselt nachgezogen");
                    _migrating = true;
                    try { WriteModel(m); }
                    finally { _migrating = false; }
                }
                return m;
            }
        }
        catch (JsonException ex)
        {
            // Inhalt ist kaputt: Datei sichern statt sie beim nächsten Speichern zu überschreiben.
            Log.Error(ex, "Einstellungen sind defekt und werden quarantänisiert: {0}", SettingsPath);
            JsonFileStore.Quarantine(SettingsPath);
        }
        catch (Exception ex)
        {
            // IO-Fehler (gesperrt, Rechte, Laufwerk weg): Inhalt ist intakt — NICHT anfassen.
            Log.Warn("Einstellungen konnten nicht gelesen werden: {0}", ex.Message);
        }
        return new Model();
    }

    private static void WriteModel(Model m)
    {
        try
        {
            // Auf Platte gehören die Geheimwerte nur verschlüsselt. Das Modell selbst bleibt
            // im Klartext (Aufrufer arbeiten damit weiter), deshalb Kopie serialisieren.
            var onDisk = m.CloneForDisk();
            Encrypt(onDisk);
            JsonFileStore.WriteAtomic(SettingsPath, JsonSerializer.Serialize(onDisk, Options));
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
        config.Maximized = m.Maximized;
        config.SessionPersist = m.SessionPersist;
        config.SessionFile = m.SessionFile;
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
        m.Maximized = config.Maximized;
        m.SessionPersist = config.SessionPersist;
        m.SessionFile = config.SessionFile;
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
        ai.Hints = m.AiHints;
        ai.SendHints = m.AiSendHints;
    }

    public static void SaveAi(AiOptions ai)
    {
        // Read-modify-write, damit der Browser-Abschnitt erhalten bleibt.
        var m = ReadModel();
        m.AiProvider = ai.Provider;
        m.AiApiKey = ai.ApiKey;
        m.AiModel = ai.Model;
        m.AiBaseUrl = ai.BaseUrl;
        m.AiHints = ai.Hints;
        m.AiSendHints = ai.SendHints;
        WriteModel(m);
    }

    public static List<string> LoadRecentFiles() => ReadModel().RecentFiles;

    public static void SaveRecentFiles(List<string> files)
    {
        var m = ReadModel();
        m.RecentFiles = files;
        WriteModel(m);
    }

    public static List<string> LoadDisabledPlugins() => ReadModel().DisabledPlugins;

    public static void SaveDisabledPlugins(List<string> files)
    {
        var m = ReadModel();
        m.DisabledPlugins = files;
        WriteModel(m);
    }

    public static bool LoadSuggestionsEnabled() => ReadModel().SuggestionsEnabled;

    public static void SaveSuggestionsEnabled(bool enabled)
    {
        var m = ReadModel();
        m.SuggestionsEnabled = enabled;
        WriteModel(m);
    }

    /// <summary>Gespeicherter UI-Sprachcode (z. B. "de", "en"). Standard: "de".</summary>
    public static string LoadLanguage() => ReadModel().Language;

    public static void SaveLanguage(string lang)
    {
        var m = ReadModel();
        m.Language = lang;
        WriteModel(m);
    }
}
