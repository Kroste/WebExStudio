using System.Text.Json;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Core.Serialization;

/// <summary>
/// Converts a legacy (Python WebEX) project into a single wired v2 <see cref="FlowDocument2"/>.
///
/// Structure produced:
///  - Main tab: <c>function</c> (targets payload) → <c>foreach</c>; the foreach's "Element"
///    output (port 0) wires to a <c>call(start)</c> node. The "fertig" output (port 1) is free.
///  - Every referenced .json file becomes a uniquely-named subnode (Name = dotted path), and
///    references (call / then_actions_file / else_actions_file) become <c>call</c> nodes.
///  - <c>if</c> uses 2 outputs (0 = then, 1 = else); inline branches are wired on the same tab
///    and rejoin the next action automatically (control-flow-graph builder).
/// </summary>
public static class LegacyImporter
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly record struct Exit(string NodeId, int Port);

    public static FlowDocument2 Convert(string projectDir)
    {
        var doc = new FlowDocument2();
        var ctx = new Ctx(doc, projectDir);
        var main = new FlowTab { Id = "main", Label = "Main", IsSubFlow = false };
        doc.Tabs.Add(main);

        var func = NewNode("function", main.Id, ctx);
        func.Config["payload"] = BuildTargetsPayload(projectDir);

        var fe = NewNode("foreach", main.Id, ctx);
        fe.Config["items"] = "{payload.targets}";
        fe.Config["ctx_key"] = "target";
        Wire(func, 0, fe);

        // foreach "Element" (port 0) → call(start)
        var startTab = GetOrCreateSubnode(Path.Combine(projectDir, "actions", "start.json"), ctx);
        var callStart = NewNode("call", main.Id, ctx);
        callStart.Config["target"] = startTab.Name ?? "start";
        Wire(fe, 0, callStart);

        foreach (var tab in doc.Tabs.ToList())
            Layout(doc, tab.Id);

        Log.Info("LegacyImport: {0} Tabs, {1} Nodes erzeugt", doc.Tabs.Count, doc.Nodes.Count);
        return doc;
    }

    // ── Subnodes ─────────────────────────────────────────────────────────────

    private static FlowTab GetOrCreateSubnode(string filePath, Ctx ctx)
    {
        var full = Path.GetFullPath(filePath);
        if (ctx.FileTabs.TryGetValue(full, out var existing))
            return ctx.Doc.GetTab(existing)!;

        var tab = new FlowTab
        {
            Id = NewId(), Name = SubnodeName(full, ctx.ProjectDir), Label = LabelFor(full),
            IsSubFlow = true,
        };
        ctx.Doc.Tabs.Add(tab);
        ctx.FileTabs[full] = tab.Id; // register before recursing (guard)

        if (File.Exists(full))
            Build(LoadActions(full), ctx, tab.Id);
        else
            Log.Warn("LegacyImport: Datei nicht gefunden: {0}", full);

        return tab;
    }

    private static string SubnodeName(string fullPath, string projectDir)
    {
        var actionsRoot = Path.GetFullPath(Path.Combine(projectDir, "actions"));
        var rel = Path.GetRelativePath(actionsRoot, fullPath);
        rel = rel[..^Path.GetExtension(rel).Length];
        return rel.Replace(Path.DirectorySeparatorChar, '.').Replace('/', '.');
    }

    private static string LabelFor(string fullPath)
    {
        var b = Path.GetFileNameWithoutExtension(fullPath);
        return b.Length == 0 ? b : char.ToUpperInvariant(b[0]) + b[1..];
    }

    // ── Control-flow-graph builder ─────────────────────────────────────────────

    /// <summary>Builds a sequence as a wired chain; returns the entry id and the open exits.</summary>
    private static (string? entry, List<Exit> exits) Build(List<JsonElement> actions, Ctx ctx, string tabId)
    {
        string? entry = null;
        var prev = new List<Exit>();
        foreach (var a in actions)
        {
            var (aEntry, aExits) = BuildNode(a, ctx, tabId);
            entry ??= aEntry;
            if (aEntry is not null)
                foreach (var ex in prev) WireById(ex.NodeId, ex.Port, aEntry, ctx);
            prev = aExits;
        }
        return (entry, prev);
    }

    private static (string? entry, List<Exit> exits) BuildNode(JsonElement a, Ctx ctx, string tabId)
    {
        // Umbenannte/zusammengeführte Node-Typen auf die aktuellen kanonischen abbilden.
        var type = CanonicalType(Str(a, "type"));
        var node = NewNode(type, tabId, ctx);
        var cfg = node.Config;
        if (string.Equals(Str(a, "type"), "open_tab", StringComparison.OrdinalIgnoreCase))
            cfg["new_tab"] = "true"; // open_tab → goto im neuen Tab

        switch (type.ToLowerInvariant())
        {
            case "click":
                cfg["selector"] = ResolveSelector(a);
                CopyIf(a, "text", cfg, "text");
                return (node.Id, [new(node.Id, 0)]);

            case "send_keys":
                cfg["selector"] = ResolveSelector(a);
                CopyIf(a, "value", cfg, "value");
                return (node.Id, [new(node.Id, 0)]);

            case "goto":
                CopyIf(a, "url", cfg, "url");
                CopyIf(a, "wait_ms", cfg, "wait_ms");
                return (node.Id, [new(node.Id, 0)]);

            case "sleep":
                CopyIf(a, "seconds", cfg, "seconds");
                return (node.Id, [new(node.Id, 0)]);

            case "menu_path":
                if (a.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    cfg["path"] = string.Join(", ", items.EnumerateArray().Select(e => e.GetString() ?? ""));
                else CopyIf(a, "path", cfg, "path");
                CopyIf(a, "open_strategy", cfg, "open_strategy");
                CopyIf(a, "match", cfg, "match");
                CopyIf(a, "click_last", cfg, "click_last");
                return (node.Id, [new(node.Id, 0)]);

            case "call":
            {
                var file = Str(a, "actions_file");
                if (string.IsNullOrEmpty(file)) file = Str(a, "file");
                if (!string.IsNullOrEmpty(file))
                    cfg["target"] = GetOrCreateSubnode(ResolvePath(ctx.ProjectDir, file), ctx).Name ?? "";
                CopyIf(a, "allow_quit", cfg, "allow_quit");
                return (node.Id, [new(node.Id, 0)]);
            }

            case "if_then_else":
            {
                BuildCondition(a, cfg);
                var exits = new List<Exit>();
                exits.AddRange(BuildBranch(a, ctx, tabId, node, 0, "then", "then_actions_file"));
                exits.AddRange(BuildBranch(a, ctx, tabId, node, 1, "else", "else_actions_file"));
                return (node.Id, exits);
            }

            case "foreach":
            case "for_range":
            case "get_links":
            {
                var exits = BuildBranch(a, ctx, tabId, node, 0, "actions", "actions_file"); // body
                exits.Add(new(node.Id, 1)); // done output
                // copy any scalar config (start/end/items/...)
                foreach (var p in a.EnumerateObject())
                    if (p.Name is not ("type" or "actions" or "actions_file") && !p.Name.StartsWith('_'))
                        cfg[p.Name] = ToStr(p.Value);
                return (node.Id, exits);
            }

            default:
                foreach (var p in a.EnumerateObject())
                    if (p.Name is not ("type" or "condition" or "then" or "else"
                        or "actions" or "then_actions_file" or "else_actions_file" or "actions_file" or "items")
                        && !p.Name.StartsWith('_'))
                        cfg[p.Name] = ToStr(p.Value);
                return (node.Id, [new(node.Id, 0)]);
        }
    }

    /// <summary>Builds a branch wired from owner.output[port]; returns the branch's open exits.</summary>
    private static List<Exit> BuildBranch(JsonElement a, Ctx ctx, string tabId, FlowNode owner, int port,
        string inlineKey, string fileKey)
    {
        if (a.TryGetProperty(inlineKey, out var inline) && inline.ValueKind == JsonValueKind.Array
            && inline.GetArrayLength() > 0)
        {
            var (bEntry, bExits) = Build(inline.EnumerateArray().ToList(), ctx, tabId);
            if (bEntry is not null) Wire(owner, port, ctx.Doc.GetNode(bEntry)!);
            return bExits;
        }

        if (a.TryGetProperty(fileKey, out var file) && file.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(file.GetString()))
        {
            var sub = GetOrCreateSubnode(ResolvePath(ctx.ProjectDir, file.GetString()!), ctx);
            var call = NewNode("call", tabId, ctx);
            call.Config["target"] = sub.Name ?? "";
            Wire(owner, port, call);
            return [new(call.Id, 0)];
        }

        // No branch → the owner's own output port is an open exit (rejoins the next action).
        return [new(owner.Id, port)];
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
            cfg["value"] = Str(a, "value");
            if (string.Equals(op, "matches", StringComparison.OrdinalIgnoreCase))
            {
                cfg["condition"] = "page_matches";
                cfg["regex"] = "true";
            }
            else cfg["condition"] = "page_contains";
        }
        else
        {
            cfg["condition"] = "element_exists";
            if (!string.IsNullOrEmpty(selector)) cfg["selector"] = selector;
        }
    }

    // ── Wiring ─────────────────────────────────────────────────────────────────

    private static void Wire(FlowNode from, int port, FlowNode to) => Add(from, port, to.Id);

    private static void WireById(string fromId, int port, string toId, Ctx ctx)
    {
        var from = ctx.Doc.GetNode(fromId);
        if (from is not null) Add(from, port, toId);
    }

    private static void Add(FlowNode from, int port, string toId)
    {
        while (from.Wires.Count <= port) from.Wires.Add([]);
        if (!from.Wires[port].Contains(toId)) from.Wires[port].Add(toId);
    }

    private static FlowNode NewNode(string type, string tabId, Ctx ctx)
    {
        var n = new FlowNode { Id = NewId(), Type = type, TabId = tabId, Wires = [[]] };
        ctx.Doc.Nodes.Add(n);
        return n;
    }

    // ── Layout (layered: Y by longest-path depth, X spread per depth) ───────────

    private static void Layout(FlowDocument2 doc, string tabId)
    {
        var nodes = doc.Nodes.Where(n => n.TabId == tabId).ToList();
        if (nodes.Count == 0) return;

        var preds = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var n in nodes)
            foreach (var port in n.Wires)
                foreach (var t in port)
                    if (preds.ContainsKey(t)) preds[t].Add(n.Id);

        var depth = new Dictionary<string, int>();
        var inProgress = new HashSet<string>();
        int Depth(string id)
        {
            if (depth.TryGetValue(id, out var d)) return d;
            if (!inProgress.Add(id)) return 0; // cycle guard
            var ps = preds.GetValueOrDefault(id) ?? [];
            d = ps.Count == 0 ? 0 : ps.Max(Depth) + 1;
            inProgress.Remove(id);
            return depth[id] = d;
        }
        foreach (var n in nodes) Depth(n.Id);

        foreach (var grp in nodes.GroupBy(n => depth[n.Id]).OrderBy(g => g.Key))
        {
            int col = 0;
            foreach (var n in grp)
            {
                n.X = 80 + col * 240;
                n.Y = 40 + grp.Key * 120;
                n.SeqIndex = col;
                col++;
            }
        }
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
                    foreach (var t in doc.RootElement.EnumerateArray())
                    {
                        var flat = new Dictionary<string, string>();
                        if (t.TryGetProperty("name", out var n)) flat["name"] = n.GetString() ?? "";
                        if (t.TryGetProperty("host", out var h)) flat["host"] = h.GetString() ?? "";
                        if (t.TryGetProperty("ctx", out var c) && c.ValueKind == JsonValueKind.Object)
                            foreach (var p in c.EnumerateObject()) flat[p.Name] = ToStr(p.Value);
                        targets.Add(flat);
                    }
            }
        }
        catch (Exception ex) { Log.Warn("LegacyImport: targets.json nicht lesbar: {0}", ex.Message); }

        if (targets.Count == 0)
            targets.Add(new() { ["name"] = "01-USV01", ["host"] = "10.1.10.252", ["location"] = "Beispiel", ["seconds"] = "2" });

        return JsonSerializer.Serialize(new Dictionary<string, object> { ["targets"] = targets },
            new JsonSerializerOptions { WriteIndented = true });
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

    private static string ResolvePath(string projectDir, string rel) =>
        Path.IsPathRooted(rel) ? rel : Path.Combine(projectDir, rel);

    /// <summary>Bildet alte/umbenannte Legacy-Action-Typen auf die aktuellen Node-Typen ab
    /// (z. B. open_tab → goto mit new_tab, eval_js → page_function).</summary>
    public static string CanonicalType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "navigate_to" or "open_tab" => "goto",
        "eval_js" => "page_function",
        _ => type,
    };

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
