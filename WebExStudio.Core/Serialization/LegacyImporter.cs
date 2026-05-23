using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

/// <summary>
/// Converts a legacy (Python WebEX) project into a single v2 <see cref="FlowDocument2"/>.
///
/// Structure produced:
///  - Main tab: a <c>function</c> node holding targets.json as payload → a <c>foreach</c>
///    node whose body is the <c>start</c> subnode.
///  - Every referenced .json file (call / then_actions_file / else_actions_file) becomes a
///    uniquely-named subnode (Name = dotted path under actions/, Label = PascalCase base name),
///    referenced via <c>call &lt;name&gt;</c>.
///  - Inline then/else arrays become branch tabs owned by the if node.
/// {placeholders} resolve from payload.
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

        // Main: function (targets) → foreach (body = start subnode)
        var func = new FlowNode
        {
            Type = "function", TabId = mainTab.Id, X = 320, Y = 60,
            Config = new() { ["payload"] = BuildTargetsPayload(projectDir) },
        };
        doc.Nodes.Add(func);

        var startTabId = GetOrCreateSubnode(Path.Combine(projectDir, "actions", "start.json"), ctx);

        var foreachNode = new FlowNode
        {
            Type = "foreach", TabId = mainTab.Id, X = 320, Y = 180,
            Config = new()
            {
                ["items"] = "{payload.targets}",
                ["ctx_key"] = "target",
                ["bodyTabId"] = startTabId,
            },
        };
        doc.Nodes.Add(foreachNode);
        func.Wires = [[foreachNode.Id]];

        Log.Info("LegacyImport: {0} Tabs, {1} Nodes erzeugt", doc.Tabs.Count, doc.Nodes.Count);
        return doc;
    }

    // ── Subnode creation (one tab per legacy file, deduped) ──────────────────────

    /// <summary>Ensures a named subnode exists for the given file; returns its tab id.</summary>
    private static string GetOrCreateSubnode(string filePath, Ctx ctx)
    {
        var full = Path.GetFullPath(filePath);
        if (ctx.FileTabs.TryGetValue(full, out var existing)) return existing;

        var name = SubnodeName(full, ctx.ProjectDir);
        var tab = new FlowTab
        {
            Id = NewId(), Name = name, Label = LabelFor(full),
            IsSubFlow = true, // standalone named subnode (OwnerNodeId stays null)
        };
        ctx.Doc.Tabs.Add(tab);
        ctx.FileTabs[full] = tab.Id; // register before recursing (recursion guard)

        if (File.Exists(full))
            AddSequential(LoadActions(full), ctx, tab.Id);
        else
            Log.Warn("LegacyImport: Datei nicht gefunden: {0}", full);

        return tab.Id;
    }

    /// <summary>Name = path under actions/ with '/'→'.', '.json' dropped (e.g. configuration.general.datetime.daylightSavings).</summary>
    private static string SubnodeName(string fullPath, string projectDir)
    {
        var actionsRoot = Path.GetFullPath(Path.Combine(projectDir, "actions"));
        var rel = Path.GetRelativePath(actionsRoot, fullPath);
        rel = rel[..^Path.GetExtension(rel).Length]; // drop .json
        return rel.Replace(Path.DirectorySeparatorChar, '.').Replace('/', '.');
    }

    private static string LabelFor(string fullPath)
    {
        var b = Path.GetFileNameWithoutExtension(fullPath);
        return b.Length == 0 ? b : char.ToUpperInvariant(b[0]) + b[1..];
    }

    // ── Node building ────────────────────────────────────────────────────────────

    private static void AddSequential(List<JsonElement> actions, Ctx ctx, string tabId)
    {
        int i = 0;
        foreach (var action in actions)
        {
            var node = BuildNode(action, ctx, tabId, seqIndex: i, x: 220, y: 60 + i * 110);
            ctx.Doc.Nodes.Add(node);
            i++;
        }
    }

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
                CopyIf(a, "open_strategy", cfg, "open_strategy");
                CopyIf(a, "match", cfg, "match");
                CopyIf(a, "click_last", cfg, "click_last");
                break;

            case "call":
            {
                var file = Str(a, "actions_file");
                if (string.IsNullOrEmpty(file)) file = Str(a, "file");
                if (!string.IsNullOrEmpty(file))
                {
                    var subId = GetOrCreateSubnode(ResolvePath(ctx.ProjectDir, file), ctx);
                    cfg["target"] = ctx.Doc.GetTab(subId)?.Name ?? "";
                }
                CopyIf(a, "allow_quit", cfg, "allow_quit");
                break;
            }

            case "if_then_else":
                BuildCondition(a, cfg);
                if (TryBranch(a, ctx, node, "then", "then_actions_file", out var thenTab)) cfg["thenTabId"] = thenTab;
                if (TryBranch(a, ctx, node, "else", "else_actions_file", out var elseTab)) cfg["elseTabId"] = elseTab;
                break;

            default:
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

        if (string.Equals(extract, "page_text", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(op, "matches", StringComparison.OrdinalIgnoreCase))
            {
                cfg["condition"] = "page_matches";
                cfg["value"] = Str(a, "value");
                cfg["regex"] = "true";
            }
            else // contains (default)
            {
                cfg["condition"] = "page_contains";
                cfg["value"] = Str(a, "value");
            }
        }
        else
        {
            cfg["condition"] = "element_exists";
            if (!string.IsNullOrEmpty(selector)) cfg["selector"] = selector;
        }
    }

    /// <summary>
    /// Builds a branch (then/else) tab owned by the if node. Inline arrays are converted to
    /// sequential nodes; a *_actions_file reference becomes a single call node to that subnode.
    /// </summary>
    private static bool TryBranch(JsonElement a, Ctx ctx, FlowNode ifNode, string slot, string fileKey, out string tabId)
    {
        tabId = "";

        if (a.TryGetProperty(slot, out var inline) && inline.ValueKind == JsonValueKind.Array
            && inline.GetArrayLength() > 0)
        {
            var tab = NewBranchTab(ctx, ifNode, slot);
            AddSequential(inline.EnumerateArray().ToList(), ctx, tab.Id);
            tabId = tab.Id;
            return true;
        }

        if (a.TryGetProperty(fileKey, out var file) && file.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(file.GetString()))
        {
            var subId = GetOrCreateSubnode(ResolvePath(ctx.ProjectDir, file.GetString()!), ctx);
            var subName = ctx.Doc.GetTab(subId)?.Name ?? "";
            var tab = NewBranchTab(ctx, ifNode, slot);
            ctx.Doc.Nodes.Add(new FlowNode
            {
                Type = "call", TabId = tab.Id, SeqIndex = 0, X = 220, Y = 60,
                Config = new() { ["target"] = subName },
            });
            tabId = tab.Id;
            return true;
        }

        return false;
    }

    private static FlowTab NewBranchTab(Ctx ctx, FlowNode ifNode, string slot)
    {
        var tab = new FlowTab
        {
            Id = NewId(),
            Label = slot == "then" ? "Then" : slot == "else" ? "Else" : "Body",
            IsSubFlow = true,
            OwnerNodeId = ifNode.Id,
            Slot = slot,
        };
        ctx.Doc.Tabs.Add(tab);
        return tab;
    }

    // ── targets.json → function payload ──────────────────────────────────────────

    private static string BuildTargetsPayload(string projectDir)
    {
        var targets = new List<Dictionary<string, string>>();
        var path = Path.Combine(projectDir, "targets.json");
        try
        {
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in doc.RootElement.EnumerateArray())
                    {
                        var flat = new Dictionary<string, string>();
                        if (t.TryGetProperty("name", out var n)) flat["name"] = n.GetString() ?? "";
                        if (t.TryGetProperty("host", out var h)) flat["host"] = h.GetString() ?? "";
                        if (t.TryGetProperty("ctx", out var c) && c.ValueKind == JsonValueKind.Object)
                            foreach (var p in c.EnumerateObject())
                                flat[p.Name] = ToStr(p.Value);
                        targets.Add(flat);
                    }
                }
            }
        }
        catch (Exception ex) { Log.Warn("LegacyImport: targets.json nicht lesbar: {0}", ex.Message); }

        if (targets.Count == 0)
            targets.Add(new() { ["name"] = "01-USV01", ["host"] = "10.1.10.252", ["location"] = "Beispiel", ["seconds"] = "2" });

        var payload = new Dictionary<string, object> { ["targets"] = targets };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<JsonElement> LoadActions(string path)
    {
        if (!File.Exists(path)) return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        JsonElement arr;
        if (root.ValueKind == JsonValueKind.Array) arr = root;
        else if (root.TryGetProperty("actions", out var a) && a.ValueKind == JsonValueKind.Array) arr = a;
        else return [];
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
