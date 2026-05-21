using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

public static class FlowSerializer
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static async Task<FlowDocument> LoadAsync(string path, bool applyLayout = true)
    {
        Log.Debug("Lade Flow: {0}", path);
        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<FlowDocument>(stream, FlowSerializerOptions.Default)
                  ?? new FlowDocument();
        doc.FilePath = path;
        if (applyLayout) AutoLayout(doc);
        Log.Info("Flow geladen: {0} ({1} Aktionen)", Path.GetFileName(path), doc.Actions.Count);
        return doc;
    }

    public static async Task SaveAsync(FlowDocument doc, string path)
    {
        Log.Info("Speichere Flow: {0}", path);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, doc, FlowSerializerOptions.Default);
        doc.FilePath = path;
        Log.Debug("Flow gespeichert: {0}", Path.GetFileName(path));
    }

    public static async Task<List<TargetConfig>> LoadTargetsAsync(string path)
    {
        Log.Debug("Lade Targets: {0}", path);
        await using var stream = File.OpenRead(path);
        var targets = await JsonSerializer.DeserializeAsync<List<TargetConfig>>(stream, FlowSerializerOptions.Default)
               ?? [];
        Log.Info("Targets geladen: {0} ({1} Einträge)", Path.GetFileName(path), targets.Count);
        return targets;
    }

    public static async Task SaveTargetsAsync(List<TargetConfig> targets, string path)
    {
        Log.Info("Speichere Targets: {0} ({1} Einträge)", path, targets.Count);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, targets, FlowSerializerOptions.Default);
    }

    /// <summary>
    /// Assigns grid positions to nodes that have no _ui metadata (imported Python flows).
    /// </summary>
    private static void AutoLayout(FlowDocument doc)
    {
        const double startX = 80;
        const double startY = 80;
        const double stepX = 260;
        const double stepY = 120;
        const int cols = 1;

        int i = 0;
        int laid = 0;
        foreach (var node in doc.Actions)
        {
            if (node.Ui is null)
            {
                node.EnsureUi(
                    startX + (i % cols) * stepX,
                    startY + (i / cols) * stepY);
                laid++;
            }
            i++;
        }
        if (laid > 0)
            Log.Debug("AutoLayout: {0}/{1} Nodes positioniert", laid, doc.Actions.Count);
    }
}
