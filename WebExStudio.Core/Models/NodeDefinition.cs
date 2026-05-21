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

    /// <summary>Ordered list of editable properties shown in the property panel.</summary>
    public List<PropertyDefinition> Properties { get; init; } = [];

    /// <summary>Whether this node type can contain sub-actions (loop body, branches).</summary>
    public bool HasSubActions { get; init; } = false;

    /// <summary>JSON property keys that hold sub-action arrays, e.g. ["then","else"] or ["actions"].</summary>
    public IReadOnlyList<string> SubActionKeys { get; init; } = [];
}

public sealed class PropertyDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public PropertyKind Kind { get; init; } = PropertyKind.Text;
    public string? DefaultValue { get; init; }
    public bool Required { get; init; } = false;
    public string? Placeholder { get; init; }
    /// <summary>Legacy key alias (e.g. Python format) used as read fallback when Key is absent.</summary>
    public string? Alias { get; init; }
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
