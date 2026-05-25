using System.Text.RegularExpressions;

namespace WebExStudio.AI;

/// <summary>
/// Holt das JSON-Objekt aus einer Modellantwort heraus — auch wenn es in einen ```json-Block
/// oder erklärenden Text eingebettet ist.
/// </summary>
public static partial class JsonExtractor
{
    [GeneratedRegex("```(?:json)?\\s*\\r?\\n(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FencedBlock();

    /// <summary>
    /// Liefert die JSON-Nutzlast. Bevorzugt den Inhalt eines ```json-Codeblocks (egal wo im Text) —
    /// wichtig, weil erklärender Text danach Klammern enthalten kann (z. B. {payload.link}), die
    /// sonst die „erstes {…letztes }"-Heuristik zerstören. Ohne Codeblock: erstes <c>{</c> bis
    /// letztes <c>}</c>.
    /// </summary>
    public static string Extract(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw.Trim();

        // Zuerst einen ```-Codeblock bevorzugen (begrenzt die Suche auf den Block-Inhalt).
        var fence = FencedBlock().Match(text);
        var candidate = fence.Success ? fence.Groups[1].Value : text;

        var start = candidate.IndexOf('{');
        var end = candidate.LastIndexOf('}');
        if (start >= 0 && end > start)
            return candidate[start..(end + 1)];

        return candidate.Trim();
    }
}
