using Microsoft.Playwright;
using NLog;

namespace WebExStudio.Engine;

/// <summary>
/// Fängt browser-initiierte Downloads (Klick auf „Download") ab und speichert sie mit ihrem
/// echten Dateinamen im Zielordner. Ohne das speichert Playwright Downloads nur temporär mit
/// GUID-Namen und löscht sie beim Schließen des Kontexts.
/// </summary>
public sealed class DownloadCollector(string targetDir)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly List<Task> _pending = [];
    private readonly Lock _lock = new();

    /// <summary>Hängt den Download-Handler an eine Seite (für jede neue Seite/Tab aufrufen).</summary>
    public void Attach(IPage page) => page.Download += (_, download) =>
    {
        var task = SaveAsync(download);
        lock (_lock) _pending.Add(task);
    };

    private async Task SaveAsync(IDownload download)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            var target = UniquePath(Path.Combine(targetDir, SafeName(download.SuggestedFilename)));
            Log.Info("Download startet: {0} → {1}", download.Url, target);
            await download.SaveAsAsync(target);
            Log.Info("Download gespeichert: {0}", target);
        }
        catch (Exception ex)
        {
            Log.Warn("Download fehlgeschlagen ({0}): {1}", download.Url, ex.Message);
        }
    }

    /// <summary>Wartet, bis alle gestarteten Downloads gespeichert sind (vor dem Schließen).</summary>
    public async Task WaitAllAsync()
    {
        Task[] tasks;
        lock (_lock) tasks = [.. _pending];
        if (tasks.Length == 0) return;
        Log.Info("Warte auf {0} laufende(n) Download(s)…", tasks.Length);
        try { await Task.WhenAll(tasks); } catch { /* einzelne Fehler bereits geloggt */ }
    }

    private static string SafeName(string? suggested)
    {
        var name = string.IsNullOrWhiteSpace(suggested) ? $"download-{Guid.NewGuid():N}" : suggested!;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
