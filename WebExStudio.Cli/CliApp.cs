using System.Text;
using System.Text.Json;
using WebExStudio.Core.Credentials;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Core.Validation;
using WebExStudio.Engine;
using WebExStudio.Engine.Plugins;

namespace WebExStudio.Cli;

/// <summary>
/// Headless-Runner für WebExStudio-Flows ohne GUI: <c>webex run|validate|secrets -f flow.json</c>.
/// Lädt dieselben Plugins und denselben Anmeldedaten-Tresor wie die App und nutzt den gleichen
/// Validator/Executor.
/// </summary>
public static class CliApp
{
    // Exit-Codes (cron-/CI-tauglich).
    private const int Ok = 0, RunError = 1, Usage = 2, VaultError = 3, Cancelled = 130;

    public static async Task<int> Run(string[] args)
    {
        Options o;
        try { o = Options.Parse(args); }
        catch (ArgumentException ex) { Console.Error.WriteLine("Fehler: " + ex.Message); return Usage; }

        return o.Command switch
        {
            "run" => await CmdRun(o),
            "validate" => await CmdValidate(o),
            "secrets" => await CmdSecrets(o),
            "help" => PrintUsage(toError: false),
            _ => PrintUsage(toError: true),
        };
    }

    // ── Befehle ────────────────────────────────────────────────────────────────

    private static async Task<int> CmdValidate(Options o)
    {
        var doc = await LoadFlow(o);
        if (doc is null) return Usage;
        LoadPlugins(); // sonst würden Plugin-Node-Typen als „unbekannt" gemeldet

        var result = FlowValidator.Validate(doc);
        PrintIssues(result);
        if (result.IsValid)
        {
            Console.WriteLine($"✓ Gültig ({result.Warnings.Count()} Warnung(en)).");
            return Ok;
        }
        Console.WriteLine($"✗ Ungültig: {result.Errors.Count()} Fehler.");
        return Usage;
    }

    private static async Task<int> CmdSecrets(Options o)
    {
        var doc = await LoadFlow(o);
        if (doc is null) return Usage;

        var refs = SecretReferenceScanner.Scan(doc);
        if (refs.Count == 0)
        {
            Console.WriteLine("Keine {secret[..]}-Referenzen im Flow.");
            return Ok;
        }
        Console.WriteLine($"{refs.Count} referenzierte Tresor-Werte (Werte werden NICHT angezeigt):");
        foreach (var r in refs)
            Console.WriteLine($"  - {r.Name}.{r.Field}");
        return Ok;
    }

    private static async Task<int> CmdRun(Options o)
    {
        var doc = await LoadFlow(o);
        if (doc is null) return Usage;
        LoadPlugins();

        // 1) Validieren — Fehler brechen ab (wie in der GUI).
        var validation = FlowValidator.Validate(doc);
        PrintIssues(validation);
        if (!validation.IsValid)
        {
            Console.Error.WriteLine($"✗ Abbruch: {validation.Errors.Count()} Validierungsfehler.");
            return Usage;
        }

        // 2) Tresor (im Flow eingebettet) nur entsperren, wenn der Flow Secrets verwendet.
        var refs = SecretReferenceScanner.Scan(doc);
        var vault = new CredentialVault();
        vault.Bind(doc);
        if (refs.Count > 0)
        {
            if (!vault.HasData)
            {
                Console.Error.WriteLine($"✗ Der Flow referenziert {refs.Count} Tresor-Wert(e), enthält aber "
                    + "keinen Tresor. Anmeldedaten im Editor (🔒) im Flow hinterlegen.");
                return VaultError;
            }
            var pw = ResolvePassword(o);
            if (pw is null)
            {
                Console.Error.WriteLine($"✗ Der Flow verwendet {refs.Count} Tresor-Wert(e), aber kein Passwort "
                    + "(-c, $WEBEX_VAULT_PW oder Eingabe) wurde angegeben.");
                return VaultError;
            }
            try { vault.Unlock(pw); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("✗ Tresor konnte nicht entsperrt werden: " + ex.Message);
                return VaultError;
            }
        }

        // 3) Laufkonfiguration aus Flags (CLI = standardmäßig headless).
        var config = new RunConfig
        {
            Headless = !o.Headful,
            ProjectDir = Path.GetDirectoryName(Path.GetFullPath(o.FlowPath!)) ?? Environment.CurrentDirectory,
        };
        if (o.Browser is not null) config.Browser = o.Browser;
        if (o.TimeoutMs is int tm) config.TimeoutMs = tm;
        if (o.DownloadDir is not null) config.DownloadDir = o.DownloadDir;
        foreach (var (k, v) in o.Vars) config.Ctx[k] = v;

        Func<string, string, string?>? secretLookup = vault.IsUnlocked ? vault.Get : null;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); Console.Error.WriteLine("Abbruch angefordert…"); };

        var trace = new ConsoleTrace();
        var executor = new FlowExecutor();
        var startedAt = DateTime.Now;

        Console.WriteLine($"▶ {Path.GetFileName(o.FlowPath)} — {doc.Nodes.Count} Nodes, "
            + $"{(config.Headless ? "headless" : "sichtbar")}, Browser={config.Browser}");

        try
        {
            await executor.RunDocumentAsync(doc, config, new TargetConfig { Name = "CLI", Enabled = true },
                trace, cts.Token, secretLookup: secretLookup);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("✗ Ausführung abgebrochen.");
            WriteReport(o, doc, trace, startedAt, success: false);
            return Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("✗ Ausführung fehlgeschlagen: " + ex.Message);
            WriteReport(o, doc, trace, startedAt, success: false);
            return RunError;
        }

        var success = trace.ErrorCount == 0;
        Console.WriteLine(success
            ? "✓ Ausführung abgeschlossen."
            : $"✗ Ausführung mit {trace.ErrorCount} Fehler(n) beendet.");
        WriteReport(o, doc, trace, startedAt, success);
        return success ? Ok : RunError;
    }

    // ── Helfer ───────────────────────────────────────────────────────────────

    private static async Task<FlowDocument2?> LoadFlow(Options o)
    {
        if (string.IsNullOrEmpty(o.FlowPath)) { Console.Error.WriteLine("Fehler: -f <flow.json> fehlt."); return null; }
        if (!File.Exists(o.FlowPath)) { Console.Error.WriteLine($"Fehler: Datei nicht gefunden: {o.FlowPath}"); return null; }
        try { return await FlowSerializer2.LoadAsync(o.FlowPath); }
        catch (Exception ex) { Console.Error.WriteLine("Fehler beim Laden des Flows: " + ex.Message); return null; }
    }

    private static void LoadPlugins()
    {
        var count = NodePluginLoader.LoadAndRegister(Paths.PluginDirs);
        if (count > 0) Console.WriteLine($"Plugins: {count} zusätzliche Node(s) geladen.");
    }

    private static string? ResolvePassword(Options o)
    {
        if (o.Password is not null) return o.Password;
        var env = Environment.GetEnvironmentVariable("WEBEX_VAULT_PW");
        if (!string.IsNullOrEmpty(env)) return env;
        if (Console.IsInputRedirected) return null; // nicht-interaktiv (cron) → kein Prompt
        Console.Write("Tresor-Passwort: ");
        return ReadPasswordMasked();
    }

    private static string ReadPasswordMasked()
    {
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                continue;
            }
            if (!char.IsControl(key.KeyChar)) { sb.Append(key.KeyChar); Console.Write('*'); }
        }
        return sb.ToString();
    }

    private static void PrintIssues(FlowValidationResult result)
    {
        foreach (var i in result.Issues)
            Console.WriteLine($"  {(i.Severity == FlowIssueSeverity.Error ? "✖" : "⚠")} [{i.Code}] {i.Message}"
                + (i.NodeId is null ? "" : $" (Node {i.NodeId})"));
    }

    private static void WriteReport(Options o, FlowDocument2 doc, ConsoleTrace trace, DateTime startedAt, bool success)
    {
        if (o.OutPath is null) return;
        var report = new
        {
            flow = o.FlowPath,
            startedAt,
            finishedAt = DateTime.Now,
            success,
            errorCount = trace.ErrorCount,
            nodes = trace.Terminal.Select(e => new
            {
                nodeId = e.NodeId,
                type = e.ActionType,
                status = e.Status.ToString(),
                message = e.ErrorMessage ?? e.Message,
            }),
        };
        try
        {
            File.WriteAllText(o.OutPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Bericht geschrieben: {o.OutPath}");
        }
        catch (Exception ex) { Console.Error.WriteLine("Bericht konnte nicht geschrieben werden: " + ex.Message); }
    }

    private static int PrintUsage(bool toError)
    {
        var w = toError ? Console.Error : Console.Out;
        w.WriteLine("""
            webex — WebExStudio-Flows ohne GUI ausführen

            Verwendung:
              webex run      -f <flow.json> [Optionen]   Flow ausführen (standardmäßig headless)
              webex validate -f <flow.json>              Nur validieren (kein Browser)
              webex secrets  -f <flow.json>              Benötigte {secret[..]}-Einträge auflisten

            Optionen (run):
              -f, --flow <pfad>        Pfad zur Flow-Datei (Pflicht)
              -c, --credential <pw>    Tresor-Passwort (Alternativen: $WEBEX_VAULT_PW oder Eingabe)
                  --var key=value      Startwert im Payload-Kontext (mehrfach möglich)
                  --headful            Browser sichtbar starten (sonst headless)
                  --browser <name>     chromium | firefox | webkit (Standard: chromium)
                  --timeout <ms>       Standard-Timeout je Aktion
                  --download-dir <d>   Zielordner für Downloads
                  --out <datei.json>   Lauf-Bericht als JSON schreiben
              -h, --help               Diese Hilfe

            Exit-Codes: 0 OK · 1 Lauffehler · 2 Validierung/Aufruf · 3 Tresor · 130 Abbruch

            Hinweis: KI-Nodes (ai_query) sind in der CLI nicht aktiv.
            """);
        return toError ? Usage : Ok;
    }

    /// <summary>Konfig-/Plugin-/Tresor-Pfade — identisch zur GUI (AppSettings).</summary>
    private static class Paths
    {
        private static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WebExStudio");

        public static string[] PluginDirs =>
        [
            Path.Combine(AppContext.BaseDirectory, "plugins"),
            Path.Combine(ConfigDir, "plugins"),
        ];
    }
}

/// <summary>Synchroner Trace-Empfänger: gibt Knotenausgänge in der Konsole aus und zählt Fehler.</summary>
internal sealed class ConsoleTrace : IProgress<TraceEntry>
{
    public int ErrorCount { get; private set; }
    public List<TraceEntry> Terminal { get; } = [];

    public void Report(TraceEntry e)
    {
        // „Running" ohne Meldung (reiner Start) nicht ausgeben; Retry-Hinweise (mit Message) schon.
        if (e.Status == ExecutionStatus.Running && string.IsNullOrEmpty(e.Message)) return;

        var tag = e.Status switch
        {
            ExecutionStatus.Success => "OK",
            ExecutionStatus.Error => "FEHLER",
            ExecutionStatus.Skipped => "SKIP",
            _ => "…",
        };
        var msg = e.ErrorMessage ?? e.Message;
        Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] {tag,-7} {e.ActionType} ({e.NodeId})"
            + (string.IsNullOrEmpty(msg) ? "" : "  " + msg));

        if (e.Status == ExecutionStatus.Error) ErrorCount++;
        if (e.Status is ExecutionStatus.Success or ExecutionStatus.Error or ExecutionStatus.Skipped)
            Terminal.Add(e);
    }
}

/// <summary>Geparste Kommandozeile.</summary>
internal sealed class Options
{
    public string Command { get; private set; } = "help";
    public string? FlowPath { get; private set; }
    public string? Password { get; private set; }
    public bool Headful { get; private set; }
    public string? Browser { get; private set; }
    public int? TimeoutMs { get; private set; }
    public string? DownloadDir { get; private set; }
    public string? OutPath { get; private set; }
    public Dictionary<string, string> Vars { get; } = [];

    public static Options Parse(string[] args)
    {
        var o = new Options();
        if (args.Length == 0) return o;

        var i = 0;
        // Erstes Token ohne führendes '-' ist der Befehl.
        if (!args[0].StartsWith('-')) { o.Command = args[0].ToLowerInvariant(); i = 1; }

        for (; i < args.Length; i++)
        {
            var a = args[i];
            string Next(string name) =>
                i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{name} erwartet einen Wert.");

            switch (a)
            {
                case "-h" or "--help": o.Command = "help"; return o;
                case "-f" or "--flow": o.FlowPath = Next(a); break;
                case "-c" or "--credential": o.Password = Next(a); break;
                case "--headful": o.Headful = true; break;
                case "--browser": o.Browser = Next(a); break;
                case "--download-dir": o.DownloadDir = Next(a); break;
                case "--out": o.OutPath = Next(a); break;
                case "--timeout":
                    o.TimeoutMs = int.TryParse(Next(a), out var ms)
                        ? ms : throw new ArgumentException("--timeout erwartet eine Zahl (ms).");
                    break;
                case "--var":
                    var kv = Next(a);
                    var eq = kv.IndexOf('=');
                    if (eq <= 0) throw new ArgumentException($"--var erwartet key=value, war: '{kv}'.");
                    o.Vars[kv[..eq]] = kv[(eq + 1)..];
                    break;
                default:
                    throw new ArgumentException($"Unbekannte Option: {a}");
            }
        }
        return o;
    }
}
