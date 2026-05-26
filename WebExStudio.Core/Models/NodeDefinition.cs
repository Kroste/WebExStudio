namespace WebExStudio.Core.Models;

/// <summary>
/// Static metadata describing a node type — used by the palette and property editor.
/// </summary>
public sealed class NodeDefinition
{
    public string Type { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Color { get; init; } = "#607D8B";
    public string Icon { get; init; } = "⚙";

    /// <summary>A short concrete usage example, shown in the properties panel.</summary>
    public string Example { get; init; } = string.Empty;

    /// <summary>Ordered list of editable properties shown in the property panel.</summary>
    public List<PropertyDefinition> Properties { get; init; } = [];

    /// <summary>Number of input ports (top of node). Default 1.</summary>
    public int InputPorts { get; init; } = 1;

    /// <summary>Number of output ports (bottom of node). Default 1.</summary>
    public int OutputPorts { get; init; } = 1;

    /// <summary>Labels for each output port (e.g. ["then","else"]). May be empty.</summary>
    public string[] OutputLabels { get; init; } = [];

    /// <summary>Veraltete/ersetzte Typen: weiterhin gültig (Validierung, Laden alter Flows),
    /// aber nicht in der Palette/im Hinzufügen-Menü sichtbar.</summary>
    public bool Hidden { get; init; } = false;

    /// <summary>True, wenn der Handler seine Ausgänge selbst routet (wie if_then_else). Ermöglicht
    /// Plugins eigene Verzweigungs-Nodes — der Executor folgt dann nicht automatisch dem ersten Ausgang.</summary>
    public bool RoutesOutputs { get; init; } = false;
}

public sealed class PropertyDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public PropertyKind Kind { get; init; } = PropertyKind.Text;
    public string? DefaultValue { get; init; }
    public bool Required { get; init; } = false;
    public string? Placeholder { get; init; }
    /// <summary>Legacy key aliases tried in order when Key is absent.</summary>
    public string[]? Aliases { get; init; }
}

public enum PropertyKind
{
    Text,
    MultilineText,
    Number,
    Boolean,
    Selector,
    Url,
    FilePath,
    Code,
    Dropdown,
}
