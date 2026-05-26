using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;
using WebExStudio.Engine.Plugins;
using ExecutionContext = WebExStudio.Engine.ExecutionContext;

// Markiert die Ziel-API-Version, damit der Loader bei Inkompatibilität warnt.
[assembly: WebExStudioPlugin(PluginApi.Version)]

namespace FileCheckPlugin;

/// <summary>Beispiel-Plugin: stellt den Node „Datei vorhanden?" bereit.</summary>
public sealed class FileCheckPlugin : INodePlugin
{
    public IEnumerable<NodePluginNode> CreateNodes() =>
    [
        new(Definition(), new FileExistsHandler()),
    ];

    private static NodeDefinition Definition() => new()
    {
        Type = "file_exists",
        DisplayName = "Datei vorhanden?",
        Category = "Plugins",
        Description = "Sucht in einem Ordner (leer = Download-Ordner) nach einer Datei oder einem Muster "
            + "(z. B. *.pdf). Gefunden → Ausgang 'gefunden' (Pfad landet in ctx_key), sonst 'nicht gefunden'. "
            + "Praktisch vor einem Download: prüfen, ob die Datei bereits existiert, und ihn ggf. überspringen.",
        Color = "#5D4037", Icon = "🗂",
        OutputPorts = 2, OutputLabels = ["gefunden", "nicht gefunden"],
        RoutesOutputs = true, // eigener Verzweigungs-Node
        Example = "name = bericht.pdf  →  Download-Ordner prüfen; bei Treffer den Download überspringen.",
        Properties =
        [
            new() { Key = "name", Label = "Dateiname oder Muster (z. B. *.pdf)", Kind = PropertyKind.Text, Required = true },
            new() { Key = "dir", Label = "Ordner (leer = Download-Ordner)", Kind = PropertyKind.FilePath },
            new() { Key = "ctx_key", Label = "Gefundener Pfad → Payload-Schlüssel", Kind = PropertyKind.Text, DefaultValue = "found_path" },
        ],
    };
}

/// <summary>Verhalten des „Datei vorhanden?"-Nodes.</summary>
public sealed class FileExistsHandler : IActionHandler
{
    public string Type => "file_exists";

    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node)
    {
        var name = ctx.Fmt(node.Get("name"));
        var dir = ctx.Fmt(node.Get("dir"));
        var ctxKey = node.Get("ctx_key", "found_path");

        if (string.IsNullOrWhiteSpace(dir))
            dir = string.IsNullOrWhiteSpace(ctx.Config.DownloadDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                : ctx.Config.DownloadDir;

        string? found = null;
        if (!string.IsNullOrWhiteSpace(name) && Directory.Exists(dir))
            found = Directory.EnumerateFiles(dir, name).FirstOrDefault();

        if (found is not null)
        {
            ctx.Set(ctxKey, found);
            return ctx.FollowOutput(node, 0); // gefunden
        }
        return ctx.FollowOutput(node, 1); // nicht gefunden
    }
}
