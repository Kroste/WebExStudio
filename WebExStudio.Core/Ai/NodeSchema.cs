namespace WebExStudio.Core.Ai;

/// <summary>
/// Kompakte, an ein LLM übergebbare Beschreibung eines Node-Typs — abgeleitet aus dem
/// <see cref="Models.NodeCatalog"/>. Dient als „Werkzeug-Katalog“, damit das Modell nur
/// existierende Typen, Pflichtfelder und Port-Semantik verwendet.
/// </summary>
public sealed record NodeSchema(
    string Type,
    string Category,
    string Description,
    string Example,
    int InputPorts,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<NodePropertySchema> Properties);

/// <summary>Beschreibung einer einzelnen Node-Eigenschaft (Config-Schlüssel).</summary>
public sealed record NodePropertySchema(
    string Key,
    string Label,
    string Kind,
    bool Required,
    string? Default,
    IReadOnlyList<string>? Aliases);
