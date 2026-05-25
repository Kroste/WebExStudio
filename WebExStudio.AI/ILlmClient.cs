namespace WebExStudio.AI;

/// <summary>
/// Provider-unabhängige Abstraktion eines Text-Modells. Implementierungen (Anthropic, OpenAI,
/// Ollama …) sprechen ihre jeweilige HTTP-API; der Flow-Generator kennt nur diese Schnittstelle.
/// </summary>
public interface ILlmClient
{
    /// <summary>Anzeigename des Anbieters/Modells (für Logs und UI).</summary>
    string Name { get; }

    /// <summary>
    /// Schickt System- und Benutzer-Prompt an das Modell und liefert dessen Textantwort.
    /// Die Antwort soll JSON enthalten (ggf. in Markdown-Fences) — das Extrahieren übernimmt
    /// der Aufrufer.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
