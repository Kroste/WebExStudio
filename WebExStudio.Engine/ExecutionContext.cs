using System.Collections.Immutable;
using System.Text;
using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;

namespace WebExStudio.Engine;

/// <summary>Eine KI-Anfrage des ai_query-Nodes. Provider/Model leer = Standard aus den Einstellungen.</summary>
public sealed record AiRequest(string SystemPrompt, string UserPrompt, bool JsonMode,
    string? Provider = null, string? Model = null);

/// <summary>
/// Holds all runtime state for a single execution.
/// There is a single data store — the payload — that flows through wires and
/// is read/written by all nodes. Placeholders {key} and {payload.key} both
/// resolve against the payload.
/// </summary>
public sealed class ExecutionContext
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IProgress<TraceEntry>? _progress;

    /// <summary>The active page. Handlers like open_tab/close_tab may switch it.</summary>
    public IPage Page { get; set; }
    public TargetConfig Target { get; }
    public RunConfig Config { get; }
    public string ProjectDir { get; }
    public ImmutableHashSet<string> CallStack { get; }
    public CancellationToken CancellationToken { get; }

    /// <summary>The single data store: payload flowing through the flow.</summary>
    public Dictionary<string, string> Payload { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The document being executed — used for sub-tab lookup.</summary>
    public FlowDocument2? Document { get; init; }

    /// <summary>Callback to execute a sub-flow tab (used by call to run a named subnode).</summary>
    public Func<string, ExecutionContext, Task>? RunSubTabCallback { get; init; }

    /// <summary>Callback to traverse the targets wired to a node's output port.</summary>
    public Func<FlowNode, int, ExecutionContext, Task>? FollowOutputCallback { get; init; }

    /// <summary>Follows the wires on the given output port (used by if/foreach to route).</summary>
    public Task FollowOutput(FlowNode node, int port, ExecutionContext? with = null) =>
        FollowOutputCallback?.Invoke(node, port, with ?? this) ?? Task.CompletedTask;

    /// <summary>Callback to pause execution until the user resumes (used by the debug node).</summary>
    public Func<string, Task>? PauseCallback { get; init; }

    /// <summary>Pauses the flow (if a pause callback is wired) until the user resumes.</summary>
    public Task Pause(string message) => PauseCallback?.Invoke(message) ?? Task.CompletedTask;

    /// <summary>Gate, das der Executor vor jedem Node abwartet — für manuelles Pausieren.
    /// Bekommt den anstehenden Node, damit die UI ihn als „nächsten" markieren kann.</summary>
    public Func<FlowNode, Task>? PauseGate { get; init; }

    /// <summary>Wartet, solange manuell pausiert wurde (sonst kehrt es sofort zurück).</summary>
    public Task CheckPauseAsync(FlowNode node) => PauseGate?.Invoke(node) ?? Task.CompletedTask;

    /// <summary>Hängt den Download-Handler an eine neu geöffnete Seite (z. B. aus open_tab).</summary>
    public Action<IPage>? AttachDownloads { get; init; }

    /// <summary>Speichert einen erkannten Download im Zielordner (für expect_download-Klicks).</summary>
    public Func<IDownload, Task>? SaveDownload { get; init; }

    /// <summary>Schickt eine <see cref="AiRequest"/> an die KI und liefert die Antwort. Von der UI mit
    /// dem konfigurierten LLM-Client verdrahtet (null = KI nicht verfügbar). Der Request kann Anbieter/
    /// Modell überschreiben (für die Auswahl im ai_query-Node).</summary>
    public Func<AiRequest, CancellationToken, Task<string>>? AiComplete { get; init; }

    /// <summary>Liefert (Name, Feld) → Wert aus dem entsperrten Credential-Tresor (null = nicht verfügbar).
    /// Von der UI verdrahtet; löst <c>{secret[name].field}</c>-Platzhalter zur Laufzeit auf.</summary>
    public Func<string, string, string?>? SecretLookup { get; init; }

    /// <summary>Alle in diesem Lauf aufgelösten Secret-Werte — geteilt über Kind-Kontexte, damit sie
    /// in allen Logs/Traces maskiert werden können. Niemals serialisieren/ausgeben.</summary>
    public HashSet<string> SecretValues { get; init; } = [];

    private const string SecretPrefix = "secret[";
    private const string PayloadPrefix = "payload.";

    public ExecutionContext(
        IPage page,
        TargetConfig target,
        RunConfig config,
        string projectDir,
        Dictionary<string, string>? payload = null,
        ImmutableHashSet<string>? callStack = null,
        IProgress<TraceEntry>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Page = page;
        Target = target;
        Config = config;
        ProjectDir = projectDir;
        CallStack = callStack ?? ImmutableHashSet<string>.Empty;
        _progress = progress;
        CancellationToken = cancellationToken;

        if (payload != null)
            foreach (var kv in payload) Payload[kv.Key] = kv.Value;
    }

    /// <summary>
    /// Ersetzt <c>{key}</c> und <c>{payload.key}</c> durch Payload-Werte.
    /// Löst KEINE <c>{secret[..]}</c>-Verweise auf — die bleiben wörtlich stehen.
    /// </summary>
    public string Fmt(string? value) => Expand(value, withSecrets: false, withPayload: true);

    /// <summary>Wie <see cref="Fmt"/>, löst zusätzlich <c>{secret[name].field}</c> aus dem Tresor auf.
    /// NUR in seitenwirksamen Nodes verwenden (Text eingeben, goto-URL …) — Secret-Werte gelangen so
    /// nie in den Payload.</summary>
    public string FmtSecret(string? value) => Expand(value, withSecrets: true, withPayload: true);

    /// <summary>Ersetzt nur <c>{secret[name].field}</c> durch die Tresor-Werte; Payload-Platzhalter
    /// bleiben unangetastet. Wirft, wenn der Tresor gesperrt ist oder ein Eintrag fehlt.
    /// Aufgelöste Werte werden für die Maskierung vermerkt.</summary>
    public string ResolveSecrets(string text) => Expand(text, withSecrets: true, withPayload: false);

    /// <summary>
    /// Löst alle Platzhalter in EINEM Durchlauf über die Vorlage auf.
    ///
    /// WARUM ein Durchlauf und nicht nacheinander ersetzen — zwei Gründe, beide unangenehm:
    ///
    /// 1. <b>Sicherheit.</b> Vorher war <c>FmtSecret</c> = „erst Payload einsetzen, dann Secrets
    ///    auflösen". Damit wurde eingesetzter Payload-Inhalt erneut nach <c>{secret[..]}</c>
    ///    durchsucht — und Payload-Inhalt kommt über <c>get_value</c> direkt von der besuchten
    ///    Seite. Eine Seite mit dem Text <c>{secret[github].password}</c> konnte sich so das echte
    ///    Passwort in ein Eingabefeld tippen oder in eine URL hängen lassen. Hier wird nur die
    ///    Vorlage gescannt; eingesetzter Text wird NIE erneut betrachtet.
    /// 2. <b>Kosten und Vorhersagbarkeit.</b> Der alte Weg lief einmal je Payload-Schlüssel über
    ///    den ganzen String (zwei <c>Replace</c> pro Schlüssel, auch ohne einen einzigen
    ///    Platzhalter in der Vorlage) und ersetzte dabei auch in bereits eingesetztem Text — das
    ///    Ergebnis hing an der Aufzählungsreihenfolge des Dictionary.
    ///
    /// Unbekannte Platzhalter bleiben wörtlich stehen (<c>"kein {fehlt}"</c>), damit man im
    /// Ergebnis sieht, was nicht aufgelöst werden konnte.
    /// </summary>
    private string Expand(string? template, bool withSecrets, bool withPayload)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (!template.Contains('{')) return template;

        var sb = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            if (template[i] != '{')
            {
                sb.Append(template[i]);
                i++;
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                sb.Append(template, i, template.Length - i); // offene Klammer: Rest wörtlich
                break;
            }

            // Verschachtelte Klammer (z. B. JSON-Text `{ "a": "{payload.x}" }`): das äußere '{'
            // gehört nicht zu einem Platzhalter — wörtlich ausgeben und beim inneren weitersuchen.
            var nextOpen = template.IndexOf('{', i + 1);
            if (nextOpen >= 0 && nextOpen < close)
            {
                sb.Append(template, i, nextOpen - i);
                i = nextOpen;
                continue;
            }

            if (TryResolveToken(template.AsSpan(i + 1, close - i - 1), withSecrets, withPayload, out var resolved))
                sb.Append(resolved);
            else
                sb.Append(template, i, close - i + 1); // unbekannt → Platzhalter bleibt stehen

            i = close + 1;
        }
        return sb.ToString();
    }

    /// <summary>Löst einen einzelnen Platzhalter-Inhalt (ohne die Klammern) auf.</summary>
    private bool TryResolveToken(ReadOnlySpan<char> token, bool withSecrets, bool withPayload, out string value)
    {
        value = string.Empty;

        if (token.StartsWith(SecretPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!TrySplitSecretRef(token, out var name, out var field)) return false;
            // Ohne Secret-Erlaubnis bleibt der Verweis stehen — so landet er nie im Payload.
            if (!withSecrets) return false;

            value = SecretLookup?.Invoke(name, field)
                ?? throw new InvalidOperationException(
                    $"Secret '{name}.{field}' nicht verfügbar (Tresor gesperrt oder Eintrag fehlt).");
            if (value.Length > 0) SecretValues.Add(value);
            return true;
        }

        if (!withPayload) return false;

        var key = token.StartsWith(PayloadPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[PayloadPrefix.Length..]
            : token;
        return Payload.TryGetValue(key.ToString(), out value!);
    }

    /// <summary>Zerlegt <c>secret[name].field</c>. Dieselbe Form wie früher der reguläre Ausdruck:
    /// Name ohne <c>]</c> und ohne Zeilenumbruch, Feld nur Buchstaben/Ziffern/Unterstrich.</summary>
    private static bool TrySplitSecretRef(ReadOnlySpan<char> token, out string name, out string field)
    {
        name = field = string.Empty;

        var close = token.IndexOf(']');
        if (close <= SecretPrefix.Length) return false;                 // "secret[]" hat keinen Namen
        if (close + 1 >= token.Length || token[close + 1] != '.') return false;

        var rawName = token[SecretPrefix.Length..close];
        if (rawName.ContainsAny('\r', '\n')) return false;

        var rawField = token[(close + 2)..];
        if (rawField.IsEmpty) return false;
        foreach (var ch in rawField)
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '_') return false;

        name = rawName.Trim().ToString();
        field = rawField.Trim().ToString();
        return true;
    }

    /// <summary>Ersetzt bekannte Secret-Werte durch *** (für sicheres Logging).</summary>
    public string MaskSecrets(string text)
    {
        if (string.IsNullOrEmpty(text) || SecretValues.Count == 0) return text;
        foreach (var s in SecretValues)
            if (s.Length > 0) text = text.Replace(s, "***");
        return text;
    }

    public string Get(string key, string fallback = "") =>
        Payload.TryGetValue(key, out var v) ? v : fallback;

    public void Set(string key, string value) => Payload[key] = value;

    public IReadOnlyDictionary<string, string> ContextSnapshot() =>
        new Dictionary<string, string>(Payload);

    /// <summary>Executes all sequential nodes in the given sub-flow tab.</summary>
    public async Task RunSubTab(string tabId)
    {
        if (RunSubTabCallback is null || Document is null) return;
        Log.Debug("RunSubTab: {0}", tabId);
        await RunSubTabCallback(tabId, this);
    }

    /// <summary>Creates a child context with extra payload values (e.g. loop variables).</summary>
    public ExecutionContext CreateChild(Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack, _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
            FollowOutputCallback = FollowOutputCallback,
            PauseCallback = PauseCallback,
            PauseGate = PauseGate,
            AttachDownloads = AttachDownloads,
            SaveDownload = SaveDownload,
            AiComplete = AiComplete,
            SecretLookup = SecretLookup,
            SecretValues = SecretValues,
        };

    /// <summary>Creates a child context for a called tab, adding tabId to the callstack.</summary>
    public ExecutionContext CreateCallChild(string calleeTabId, Dictionary<string, string>? extra = null) =>
        new(Page, Target, Config, ProjectDir, MergeWith(extra),
            CallStack.Add(calleeTabId), _progress, CancellationToken)
        {
            Document = Document,
            RunSubTabCallback = RunSubTabCallback,
            FollowOutputCallback = FollowOutputCallback,
            PauseCallback = PauseCallback,
            PauseGate = PauseGate,
            AttachDownloads = AttachDownloads,
            SaveDownload = SaveDownload,
            AiComplete = AiComplete,
            SecretLookup = SecretLookup,
            SecretValues = SecretValues,
        };

    public void Report(TraceEntry entry) => _progress?.Report(entry);

    private Dictionary<string, string> MergeWith(Dictionary<string, string>? extra)
    {
        var merged = new Dictionary<string, string>(Payload, StringComparer.OrdinalIgnoreCase);
        if (extra != null)
            foreach (var kv in extra) merged[kv.Key] = kv.Value;
        return merged;
    }
}
