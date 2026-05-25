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
}
