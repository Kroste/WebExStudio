namespace WebExStudio.AI;

/// <summary>Konfiguration der KI-Anbindung (aus den App-Einstellungen befüllt).</summary>
public sealed class AiOptions
{
    /// <summary>Anbieter-Kennung: <c>anthropic</c>, <c>openai</c> oder <c>ollama</c>.</summary>
    public string Provider { get; set; } = "anthropic";

    /// <summary>API-Schlüssel (bei Ollama nicht nötig).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modellname. Leer = Standardmodell des Anbieters.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Abweichende Basis-URL (z. B. lokale Ollama-Instanz oder Proxy). Leer = Standard.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>True, wenn die Anbindung nutzbar konfiguriert ist.</summary>
    public bool IsConfigured =>
        Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Kurze, vom Nutzer gepflegte Hinweise / bekannte Probleme &amp; Lösungen (eine pro Zeile),
    /// die der KI mit jeder Anfrage mitgegeben werden.
    /// </summary>
    public string Hints { get; set; } = DefaultHints;

    /// <summary>Ob die Hinweise an die KI mitgesendet werden.</summary>
    public bool SendHints { get; set; } = true;

    /// <summary>Die effektiv zu sendenden Hinweise, oder null wenn deaktiviert/leer.</summary>
    public string? ActiveHints =>
        SendHints && !string.IsNullOrWhiteSpace(Hints) ? Hints.Trim() : null;

    /// <summary>Sinnvolle Start-Hinweise (kurz, aus bisher gelösten Problemen).</summary>
    public const string DefaultHints =
        "- if_then_else: Vergleichswert gehört in 'value', DOM-Selektor in 'selector'. "
        + "URL-Prüfung: condition=page_url, value=z. B. mega.nz.\n"
        + "- f95zone-Maskierte Links (…/masked/<host>/…) enthalten den Zielhost im Pfad; "
        + "nach open_tab kurz warten (sleep), dann mit page_url auf den Host prüfen.\n"
        + "- Download-Buttons mit einem click-Node + expect_download=true anklicken, damit die "
        + "Datei gespeichert wird (sonst geht der Download verloren).";
}
