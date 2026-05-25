namespace WebExStudio.AI;

/// <summary>
/// Holt das JSON-Objekt aus einer Modellantwort heraus — auch wenn es in einen ```json-Block
/// oder erklärenden Text eingebettet ist.
/// </summary>
public static class JsonExtractor
{
    /// <summary>
    /// Liefert die JSON-Nutzlast (vom ersten <c>{</c> bis zur zugehörigen schließenden <c>}</c>)
    /// oder den getrimmten Originaltext, falls keine Klammern gefunden werden.
    /// </summary>
    public static string Extract(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw.Trim();

        // Markdown-Codeblock entfernen, falls vorhanden.
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0) text = text[..fenceEnd];
            text = text.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return text;
    }
}
