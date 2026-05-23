using System.Text.Json;
using NLog;
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
    }

    public static void Load(RunConfig config)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var m = JsonSerializer.Deserialize<Model>(File.ReadAllText(SettingsPath), Options);
            if (m is null) return;
            config.Browser = m.Browser;
            config.BrowserChannel = m.BrowserChannel;
            config.BrowserExecutablePath = m.BrowserExecutablePath;
            config.DriverPath = m.DriverPath;
            config.Headless = m.Headless;
            Log.Info("Einstellungen geladen: {0}", SettingsPath);
        }
        catch (Exception ex)
        {
            Log.Warn("Einstellungen konnten nicht geladen werden: {0}", ex.Message);
        }
    }

    public static void Save(RunConfig config)
    {
        try
        {
            var m = new Model
            {
                Browser = config.Browser,
                BrowserChannel = config.BrowserChannel,
                BrowserExecutablePath = config.BrowserExecutablePath,
                DriverPath = config.DriverPath,
                Headless = config.Headless,
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(m, Options));
            Log.Info("Einstellungen gespeichert: {0}", SettingsPath);
        }
        catch (Exception ex)
        {
            Log.Warn("Einstellungen konnten nicht gespeichert werden: {0}", ex.Message);
        }
    }
}
