using WebExStudio.Core.Models;

namespace WebExStudio.Core.Validation;

/// <summary>
/// Prüft ein <see cref="FlowDocument2"/> auf strukturelle und schematische Fehler.
/// Gedacht als Sicherheitsnetz für importierte oder (künftig) KI-erzeugte Flows,
/// aber auch nützlich vor dem Speichern/Ausführen.
/// </summary>
public static class FlowValidator
{
    public static FlowValidationResult Validate(FlowDocument2 doc)
    {
        var result = new FlowValidationResult();
        var tabIds = doc.Tabs.Select(t => t.Id).ToHashSet();
        var nodesById = new Dictionary<string, FlowNode>();

        ValidateTabs(doc, result);
        ValidateNodeIdsUnique(doc, result, nodesById);

        foreach (var node in doc.Nodes)
            ValidateNode(doc, node, tabIds, nodesById, result);

        ValidateGroups(doc, nodesById, result);
        ValidateEntryPoints(doc, result);

        return result;
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    private static void ValidateTabs(FlowDocument2 doc, FlowValidationResult result)
    {
        if (!doc.Tabs.Any(t => !t.IsSubFlow))
            result.Add(FlowIssueSeverity.Error, "no-main-tab",
                "Es gibt keinen Haupt-Tab (alle Tabs sind Subnodes).");

        // Doppelte Subnode-Namen machen call-Ziele mehrdeutig.
        foreach (var clash in doc.Tabs
                     .Where(t => !string.IsNullOrEmpty(t.Name))
                     .GroupBy(t => t.Name!, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
            result.Add(FlowIssueSeverity.Error, "duplicate-subnode-name",
                $"Subnode-Name '{clash.Key}' ist {clash.Count()}× vergeben.", tabId: clash.First().Id);
    }

    private static void ValidateNodeIdsUnique(FlowDocument2 doc, FlowValidationResult result,
        Dictionary<string, FlowNode> nodesById)
    {
        foreach (var node in doc.Nodes)
        {
            if (!nodesById.TryAdd(node.Id, node))
                result.Add(FlowIssueSeverity.Error, "duplicate-node-id",
                    $"Node-ID '{node.Id}' kommt mehrfach vor.", node.Id, node.TabId);
        }
    }

    // ── Einzelner Node ─────────────────────────────────────────────────────────

    private static void ValidateNode(FlowDocument2 doc, FlowNode node, HashSet<string> tabIds,
        Dictionary<string, FlowNode> nodesById, FlowValidationResult result)
    {
        if (!tabIds.Contains(node.TabId))
            result.Add(FlowIssueSeverity.Error, "unknown-tab",
                $"Node '{node.Id}' verweist auf unbekannten Tab '{node.TabId}'.", node.Id, node.TabId);

        var def = NodeCatalog.Get(node.Type);
        if (def is null)
        {
            result.Add(FlowIssueSeverity.Error, "unknown-type",
                $"Unbekannter Node-Typ '{node.Type}' (Node '{node.Id}').", node.Id, node.TabId);
            // Struktur (Wires) trotzdem prüfen, Schema (Properties/Ports) nicht.
            ValidateWireTargets(node, nodesById, def: null, result);
            return;
        }

        ValidateRequiredProperties(node, def, result);
        ValidateWireTargets(node, nodesById, def, result);
        ValidateCallTarget(doc, node, result);
    }

    private static void ValidateRequiredProperties(FlowNode node, NodeDefinition def, FlowValidationResult result)
    {
        foreach (var prop in def.Properties.Where(p => p.Required))
        {
            if (!HasValue(node, prop))
                result.Add(FlowIssueSeverity.Error, "missing-required",
                    $"Pflichtfeld '{prop.Key}' fehlt bei Node '{node.Id}' ({node.Type}).", node.Id, node.TabId);
        }
    }

    private static void ValidateWireTargets(FlowNode node, Dictionary<string, FlowNode> nodesById,
        NodeDefinition? def, FlowValidationResult result)
    {
        for (var port = 0; port < node.Wires.Count; port++)
        {
            var targets = node.Wires[port];

            // Wire an einem Port, den es laut Katalog nicht gibt.
            if (def is not null && port >= def.OutputPorts && targets.Count > 0)
                result.Add(FlowIssueSeverity.Error, "wire-invalid-port",
                    $"Node '{node.Id}' ({node.Type}) hat Verbindungen an Ausgang {port}, " +
                    $"besitzt aber nur {def.OutputPorts} Ausgänge.", node.Id, node.TabId);

            foreach (var targetId in targets)
            {
                if (!nodesById.TryGetValue(targetId, out var target))
                {
                    result.Add(FlowIssueSeverity.Error, "dangling-wire",
                        $"Verbindung von '{node.Id}' zeigt auf nicht existierenden Node '{targetId}'.",
                        node.Id, node.TabId);
                    continue;
                }

                if (target.TabId != node.TabId)
                    result.Add(FlowIssueSeverity.Error, "cross-tab-wire",
                        $"Verbindung von '{node.Id}' führt zu '{targetId}' auf einem anderen Tab " +
                        "(Verbindungen müssen innerhalb eines Tabs bleiben; Subnodes per 'call').",
                        node.Id, node.TabId);

                var targetDef = NodeCatalog.Get(target.Type);
                if (targetDef is not null && targetDef.InputPorts == 0)
                    result.Add(FlowIssueSeverity.Error, "wire-into-no-input",
                        $"Verbindung von '{node.Id}' führt zu '{targetId}' ({target.Type}), " +
                        "der keinen Eingang besitzt.", node.Id, node.TabId);
            }
        }
    }

    private static void ValidateCallTarget(FlowDocument2 doc, FlowNode node, FlowValidationResult result)
    {
        if (!node.Type.Equals("call", StringComparison.OrdinalIgnoreCase)) return;

        var target = node.Get("target");
        if (string.IsNullOrWhiteSpace(target)) return; // bereits durch missing-required erfasst

        if (doc.GetTabByName(target) is null)
            result.Add(FlowIssueSeverity.Error, "call-target-missing",
                $"call-Node '{node.Id}' verweist auf unbekannten Subnode '{target}'.", node.Id, node.TabId);
    }

    // ── Gruppen ──────────────────────────────────────────────────────────────

    private static void ValidateGroups(FlowDocument2 doc, Dictionary<string, FlowNode> nodesById,
        FlowValidationResult result)
    {
        foreach (var group in doc.Groups)
            foreach (var nid in group.NodeIds)
            {
                if (!nodesById.TryGetValue(nid, out var n))
                    result.Add(FlowIssueSeverity.Warning, "group-missing-node",
                        $"Gruppe '{group.Label}' enthält nicht existierenden Node '{nid}'.", nid, group.TabId);
                else if (n.TabId != group.TabId)
                    result.Add(FlowIssueSeverity.Warning, "group-foreign-node",
                        $"Gruppe '{group.Label}' enthält Node '{nid}' von einem anderen Tab.", nid, group.TabId);
            }
    }

    // ── Erreichbarkeit ─────────────────────────────────────────────────────────

    private static void ValidateEntryPoints(FlowDocument2 doc, FlowValidationResult result)
    {
        foreach (var tab in doc.Tabs)
        {
            // Nur ausführbare Nodes betrachten (Annotationen haben keine Ports).
            var executable = doc.Nodes
                .Where(n => n.TabId == tab.Id && !IsAnnotation(n.Type))
                .ToList();
            if (executable.Count == 0) continue;

            var incoming = doc.BuildIncomingSet(tab.Id);
            if (executable.All(n => incoming.Contains(n.Id)))
                result.Add(FlowIssueSeverity.Warning, "no-entry-node",
                    $"Tab '{tab.Label}' hat keinen Startpunkt — jeder Node hat eine eingehende " +
                    "Verbindung (möglicher Zyklus ohne Einstieg).", tabId: tab.Id);
        }
    }

    // ── Helfer ─────────────────────────────────────────────────────────────────

    private static bool IsAnnotation(string type) =>
        type is "label" or "caption" or "note";

    private static bool HasValue(FlowNode node, PropertyDefinition prop)
    {
        if (!string.IsNullOrWhiteSpace(node.Get(prop.Key))) return true;
        if (prop.Aliases is not null)
            foreach (var alias in prop.Aliases)
                if (!string.IsNullOrWhiteSpace(node.Get(alias))) return true;
        return false;
    }
}
