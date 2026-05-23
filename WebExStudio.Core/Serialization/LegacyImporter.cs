using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

/// <summary>
/// Converts a legacy (Python WebEX) project — a start.json with nested call/then
/// files and inline if/then/else branches — into a single v2 <see cref="FlowDocument2"/>.
/// The main flow becomes a wired chain on the main tab; call targets, if/else branches
/// and loop bodies become sequential sub-flow tabs. {placeholders} resolve from payload.
/// </summary>
public static class LegacyImporter
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static FlowDocument2 Convert(string projectDir)
    {
        var doc = new FlowDocument2();
        var mainTab = new FlowTab { Id = "main", Label = "Main", IsSubFlow = false };
        doc.Tabs.Add(mainTab);

        var ctx = new Ctx(doc, projectDir);

        var startActions = LoadActions(Path.Combine(projectDir, "actions", "start.json"));

        // Start node providing the initial payload (replaces targets/ctx).
        var start = new FlowNode
        {
            Type = "function", TabId = mainTab.Id, X = 300, Y = 60,
            Config = new()
            {
                ["payload"] = BuildStartPayload(projectDir),
            },
        };
        doc.Nodes.Add(start);

        var chain = new List<FlowNode> { start };
        AddWired(startActions, ctx, mainTab.Id, chain, startY: 180);

        Log.Info("LegacyImport: {0} Tabs, {1} Nodes erzeugt", doc.Tabs.Count, doc.Nodes.Count);
        return doc;
    }

    // ── Wired (main tab) ───────────────────────────────────────────────────────

    private static void AddWired(List<JsonElement> actions, Ctx ctx, string tabId,
        List<FlowNode> chain, double startY)
    {
        double y = startY;
        foreach (var action in actions)
        {
            var node = BuildNode(action, ctx, tabId, seqIndex: 0, x: 300, y: y);
            ctx.Doc.Nodes.Add(node);
            // Wire previous → this
            chain[^1].Wires = [[node.Id]];
            chain.Add(node);
            y += 110;
        }
    }

    // ── Sequential (sub-flow tabs) ───────────────────────────────────────────────

    private static void AddSequential(List<JsonElement> actions, Ctx ctx, string tabId)
    {
        int i = 0;
        foreach (var action in actions)
        {
            var node = BuildNode(action, ctx, tabId, seqIndex: i, x: 200, y: 60 + i * 110);
            ctx.Doc.Nodes.Add(node);
            i++;
        }
    }

    // ── Node mapping ─────────────────────────────────────────────────────────────

    private static FlowNode BuildNode(JsonElement a, Ctx ctx, string tabId, int seqIndex, double x, double y)
    {
        var type = Str(a, "type");
        if (string.Equals(type, "navigate_to", StringComparison.OrdinalIgnoreCase)) type = "goto";

        var node = new FlowNode { Type = type, TabId = tabId, SeqIndex = seqIndex, X = x, Y = y };
        var cfg = node.Config;

        switch (type.ToLowerInvariant())
        {
            case "click":
                cfg["selector"] = ResolveSelector(a);
                CopyIf(a, "text", cfg, "text");
                break;

            case "send_keys":
                cfg["selector"] = ResolveSelector(a);
                CopyIf(a, "value", cfg, "value");
                break;

            case "goto":
                CopyIf(a, "url", cfg, "url");
                CopyIf(a, "wait_ms", cfg, "wait_ms");
                break;

            case "sleep":
                CopyIf(a, "seconds", cfg, "seconds");
                break;

            case "menu_path":
                if (a.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    cfg["path"] = string.Join(", ", items.EnumerateArray().Select(e => e.GetString() ?? ""));
                else
                    CopyIf(a, "path", cfg, "path");
                break;

            case "call":
                cfg["targetTabId"] = ConvertCallTarget(a, ctx);
                break;

            case "if_then_else":
                BuildCondition(a, cfg);
                if (TryBranch(a, ctx, "then", "then_actions_file", out var thenTab)) cfg["thenTabId"] = thenTab;
                if (TryBranch(a, ctx, "else", "else_actions_file", out var elseTab)) cfg["elseTabId"] = elseTab;
                break;

            default:
                // Generic: copy scalar fields into config (skip structural keys).
                foreach (var prop in a.EnumerateObject())
                {
                    if (prop.Name is "type" or "then" or "else" or "actions"
                        or "then_actions_file" or "else_actions_file" or "actions_file"
                        or "condition" or "items" || prop.Name.StartsWith('_'))
                        continue;
                    cfg[prop.Name] = ToStr(prop.Value);
                }
                break;
        }

        return node;
    }

    private static string ResolveSelector(JsonElement a)
    {
        if (a.TryGetProperty("selector", out var sel) && sel.ValueKind == JsonValueKind.String)
            return sel.GetString() ?? "";
        if (a.TryGetProperty("xpath", out var xp) && xp.ValueKind == JsonValueKind.String)
            return xp.GetString() ?? "";
        if (a.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
            return $"[name=\"{nm.GetString()}\"]";
        return "";
    }

    private static void BuildCondition(JsonElement a, Dictionary<string, string> cfg)
    {
        var op = Str(a, "op");
        var extract = "";
        var selector = "";
        if (a.TryGetProperty("condition", out var cond) && cond.ValueKind == JsonValueKind.Object)
        {
            extract = Str(cond, "extract");
            selector = Str(cond, "selector");
        }

        if (string.Equals(extract, "page_text", StringComparison.OrdinalIgnoreCase)
            && string.Equals(op, "matches", StringComparison.OrdinalIgnoreCase))
        {
            cfg["condition"] = "page_matches";
            cfg["value"] = Str(a, "value");
            cfg["regex"] = "true";
        }
        else
        {
            cfg["condition"] = "element_exists";
            if (!string.IsNullOrEmpty(selector)) cfg["selector"] = selector;
        }
    }

    private static bool TryBranch(JsonElement a, Ctx ctx, string inlineKey, string fileKey, out string tabId)
    {
        tabId = "";
        List<JsonElement>? branch = null;

        if (a.TryGetProperty(inlineKey, out var inline) && inline.ValueKind == JsonValueKind.Array
            && inline.GetArrayLength() > 0)
            branch = inline.EnumerateArray().ToList();
        else if (a.TryGetProperty(fileKey, out var file) && file.ValueKind == JsonValueKind.String)
        {
            var path = ResolvePath(ctx.ProjectDir, file.GetString() ?? "");
            if (File.Exists(path)) branch = LoadActions(path);
        }

        if (branch is null || branch.Count == 0) return false;

        var tab = new FlowTab
        {
            Id = NewId(), Label = inlineKey == "then" ? "Then" : "Else",
            IsSubFlow = true, Slot = inlineKey,
        };
        ctx.Doc.Tabs.Add(tab);
        AddSequential(branch, ctx, tab.Id);
        tabId = tab.Id;
        return true;
    }

    private static string ConvertCallTarget(JsonElement a, Ctx ctx)
    {
        var file = Str(a, "actions_file");
        if (string.IsNullOrEmpty(file)) file = Str(a, "file");
        if (string.IsNullOrEmpty(file)) return "";

        var path = ResolvePath(ctx.ProjectDir, file);
        if (ctx.FileTabs.TryGetValue(path, out var existing)) return existing;

        var label = Path.GetFileNameWithoutExtension(path);
        var tab = new FlowTab { Id = NewId(), Label = label, IsSubFlow = true, Slot = "body" };
        ctx.Doc.Tabs.Add(tab);
        ctx.FileTabs[path] = tab.Id; // register before recursing (recursion guard)

        if (File.Exists(path))
            AddSequential(LoadActions(path), ctx, tab.Id);
        else
            Log.Warn("LegacyImport: call-Datei nicht gefunden: {0}", path);

        return tab.Id;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildStartPayload(string projectDir)
    {
        // Seed payload from the first legacy target (if present) as a template.
        var targetsPath = Path.Combine(projectDir, "targets.json");
        var payload = new Dictionary<string, object>
        {
            ["host"] = "10.1.10.252",
            ["name"] = "01-USV01",
            ["location"] = "Humboldt Gymnasium",
            ["seconds"] = "2",
        };
        try
        {
            if (File.Exists(targetsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(targetsPath));
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    if (first.TryGetProperty("host", out var h)) payload["host"] = h.GetString() ?? "";
                    if (first.TryGetProperty("name", out var n)) payload["name"] = n.GetString() ?? "";
                    if (first.TryGetProperty("ctx", out var c) && c.ValueKind == JsonValueKind.Object)
                    {
                        if (c.TryGetProperty("location", out var loc)) payload["location"] = loc.GetString() ?? "";
                        if (c.TryGetProperty("seconds", out var s)) payload["seconds"] = ToStr(s);
                    }
                }
            }
        }
        catch (Exception ex) { Log.Warn("LegacyImport: targets.json nicht lesbar: {0}", ex.Message); }

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<JsonElement> LoadActions(string path)
    {
        if (!File.Exists(path)) return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        JsonElement arr;
        if (root.ValueKind == JsonValueKind.Array) arr = root;
        else if (root.TryGetProperty("actions", out var a) && a.ValueKind == JsonValueKind.Array) arr = a;
        else return [];
        // Clone elements so they survive after the JsonDocument is disposed.
        return arr.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string ResolvePath(string projectDir, string rel) =>
        Path.IsPathRooted(rel) ? rel : Path.Combine(projectDir, rel);

    private static string Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) ? ToStr(v) : "";

    private static void CopyIf(JsonElement a, string srcKey, Dictionary<string, string> cfg, string dstKey)
    {
        if (a.TryGetProperty(srcKey, out var v)) cfg[dstKey] = ToStr(v);
    }

    private static string ToStr(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => v.GetRawText(),
    };

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    private sealed class Ctx(FlowDocument2 doc, string projectDir)
    {
        public FlowDocument2 Doc { get; } = doc;
        public string ProjectDir { get; } = projectDir;
        public Dictionary<string, string> FileTabs { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
