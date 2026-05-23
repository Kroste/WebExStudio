using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

public static class FlowSerializer2
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new StringValueDictionaryConverter() },
    };

    public static async Task<FlowDocument2> LoadAsync(string path)
    {
        Log.Debug("Lade Flow v2: {0}", path);
        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<FlowDocument2>(stream, Options)
                  ?? new FlowDocument2();
        doc.FilePath = path;

        EnsureMainTab(doc);
        AutoLayout(doc);

        Log.Info("Flow v2 geladen: {0} ({1} Tabs, {2} Nodes)",
            Path.GetFileName(path), doc.Tabs.Count, doc.Nodes.Count);
        return doc;
    }

    public static async Task SaveAsync(FlowDocument2 doc, string path)
    {
        Log.Info("Speichere Flow v2: {0}", path);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, doc, Options);
        doc.FilePath = path;
        Log.Debug("Flow v2 gespeichert: {0}", Path.GetFileName(path));
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

    /// <summary>Creates a fresh empty document with a single Main tab.</summary>
    public static FlowDocument2 CreateEmpty()
    {
        var mainTab = new FlowTab { Id = NewId(), Label = "Main", IsSubFlow = false };
        return new FlowDocument2 { Tabs = [mainTab] };
    }

    private static void EnsureMainTab(FlowDocument2 doc)
    {
        if (!doc.Tabs.Any(t => !t.IsSubFlow))
            doc.Tabs.Insert(0, new FlowTab { Id = NewId(), Label = "Main", IsSubFlow = false });
    }

    private static void AutoLayout(FlowDocument2 doc)
    {
        // Group nodes by tab and auto-position those with X=0, Y=0
        var byTab = doc.Nodes.GroupBy(n => n.TabId);
        foreach (var group in byTab)
        {
            var nodes = group.ToList();
            var allZero = nodes.All(n => n.X == 0 && n.Y == 0);
            if (!allZero) continue;

            const double startX = 200;
            const double startY = 80;
            const double stepY = 120;
            int i = 0;
            foreach (var node in nodes)
            {
                node.X = startX;
                node.Y = startY + i * stepY;
                node.SeqIndex = i;
                i++;
            }
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
