using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine.Plugins;

/// <summary>Ein vom Plugin bereitgestellter Node: Metadaten (für Palette/Validierung/KI) + Verhalten.</summary>
public sealed record NodePluginNode(NodeDefinition Definition, IActionHandler Handler);

/// <summary>
/// Erweiterungspunkt für eigene Nodes. Ein Plugin referenziert WebExStudio.Core + WebExStudio.Engine,
/// implementiert diese Schnittstelle (mit parameterlosem Konstruktor) und legt seine DLL in den
/// <c>plugins/</c>-Ordner. WebExStudio lädt sie beim Start und registriert die Nodes.
/// </summary>
public interface INodePlugin
{
    /// <summary>Liefert die Nodes dieses Plugins (Definition + Handler).</summary>
    IEnumerable<NodePluginNode> CreateNodes();
}
