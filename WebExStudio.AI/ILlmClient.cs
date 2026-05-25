namespace WebExStudio.AI;

/// <summary>Rolle einer Chat-Nachricht.</summary>
public enum ChatRole { User, Assistant }

/// <summary>Eine einzelne Nachricht im Gesprächsverlauf.</summary>
public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>
/// Provider-unabhängige Abstraktion eines Text-Modells. Implementierungen (Anthropic, OpenAI,
/// Ollama …) sprechen ihre jeweilige HTTP-API; Generator und Chat kennen nur diese Schnittstelle.
/// </summary>
public interface ILlmClient
{
    /// <summary>Anzeigename des Anbieters/Modells (für Logs und UI).</summary>
    string Name { get; }

    /// <summary>
    /// Schickt System-Prompt und Gesprächsverlauf an das Modell und liefert dessen Textantwort.
    /// Mit <paramref name="jsonMode"/> wird (sofern der Anbieter es unterstützt) reine
    /// JSON-Ausgabe erzwungen.
    /// </summary>
    Task<string> ChatAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessage> messages,
        bool jsonMode = false,
        CancellationToken ct = default);
}

/// <summary>Bequemlichkeits-Erweiterungen für <see cref="ILlmClient"/>.</summary>
public static class LlmClientExtensions
{
    /// <summary>Einzelner Frage→Antwort-Aufruf mit erzwungener JSON-Ausgabe (für die Flow-Generierung).</summary>
    public static Task<string> CompleteAsync(
        this ILlmClient client, string systemPrompt, string userPrompt, CancellationToken ct = default) =>
        client.ChatAsync(systemPrompt, [new ChatMessage(ChatRole.User, userPrompt)], jsonMode: true, ct);
}
