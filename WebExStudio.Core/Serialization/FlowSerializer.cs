using System.Text.Json;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

public static class FlowSerializer
{
    public static async Task<FlowDocument> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<FlowDocument>(stream, FlowSerializerOptions.Default)
                  ?? new FlowDocument();
        doc.FilePath = path;
        AutoLayout(doc);
        return doc;
    }

    public static async Task SaveAsync(FlowDocument doc, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, doc, FlowSerializerOptions.Default);
        doc.FilePath = path;
    }

    public static async Task<List<TargetConfig>> LoadTargetsAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<TargetConfig>>(stream, FlowSerializerOptions.Default)
               ?? [];
    }

    public static async Task SaveTargetsAsync(List<TargetConfig> targets, string path)
    {
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
        foreach (var node in doc.Actions)
        {
            if (node.Ui is null)
            {
                node.EnsureUi(
                    startX + (i % cols) * stepX,
                    startY + (i / cols) * stepY);
            }
            i++;
        }
    }
}
