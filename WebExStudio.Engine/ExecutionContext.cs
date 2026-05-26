using System.Collections.Immutable;
using System.Text.RegularExpressions;
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

    private static readonly Regex SecretRefRegex =
        new(@"\{secret\[([^\]\r\n]+)\]\.([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

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

    /// <summary>Substitutes {key} and {payload.key} tokens with payload values.</summary>
    public string Fmt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var kv in Payload)
        {
            value = value.Replace($"{{payload.{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
            value = value.Replace($"{{{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
        }
        return value;
    }

    /// <summary>Wie <see cref="Fmt"/>, löst zusätzlich <c>{secret[name].field}</c> aus dem Tresor auf.
    /// NUR in seitenwirksamen Nodes verwenden (Text eingeben, goto-URL …) — Secret-Werte gelangen so
    /// nie in den Payload.</summary>
    public string FmtSecret(string? value) => ResolveSecrets(Fmt(value));

    /// <summary>Ersetzt <c>{secret[name].field}</c> durch die Tresor-Werte (für sofortige Verwendung).
    /// Wirft, wenn der Tresor gesperrt ist oder ein Eintrag fehlt. Aufgelöste Werte werden für die
    /// Maskierung vermerkt.</summary>
    public string ResolveSecrets(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{secret[", StringComparison.OrdinalIgnoreCase))
            return text;
        return SecretRefRegex.Replace(text, m =>
        {
            var name = m.Groups[1].Value.Trim();
            var field = m.Groups[2].Value.Trim();
            var val = SecretLookup?.Invoke(name, field)
                ?? throw new InvalidOperationException(
                    $"Secret '{name}.{field}' nicht verfügbar (Tresor gesperrt oder Eintrag fehlt).");
            if (val.Length > 0) SecretValues.Add(val);
            return val;
        });
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
