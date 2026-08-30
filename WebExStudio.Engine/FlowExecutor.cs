using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine;

public sealed class FlowExecutor
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Leerer Schnappschuss für die Routine-Einträge (Start/Erfolg/Übersprungen).
    ///
    /// WARUM: <see cref="ExecutionContext.ContextSnapshot"/> kopiert den kompletten Payload. Bei zwei
    /// Einträgen je Node und einer Schleife über einige tausend Elemente hängt so ein Vielfaches des
    /// Payloads im Protokoll fest, das niemand liest — angezeigt wird der Schnappschuss nur vom
    /// debug-Node, und der baut ihn sich selbst. Bei Fehlern bleibt er erhalten, dort ist er die
    /// halbe Diagnose.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NoSnapshot =
        new Dictionary<string, string>();
    private readonly ActionRegistry _registry;

    public FlowExecutor(ActionRegistry? registry = null)
    {
        _registry = registry ?? ActionRegistry.CreateDefault();
    }

    /// <summary>
    /// Runs a complete project: opens one browser, iterates all enabled targets sequentially.
    /// </summary>
    public async Task RunProjectAsync(
        RunConfig config,
        List<TargetConfig> targets,
        IProgress<TraceEntry>? progress = null,
        CancellationToken ct = default)
    {
        Log.Info("Projekt-Ausführung gestartet: {0} Targets, Browser={1}", targets.Count(t => t.Enabled), config.Browser);
        ApplyDriverPath(config);
        using var playwright = await Playwright.CreateAsync();
        var browser = await LaunchBrowserAsync(playwright, config);
        try
        {
            foreach (var target in targets.Where(t => t.Enabled))
            {
                ct.ThrowIfCancellationRequested();
                Log.Info("Target startet: {0} ({1})", target.Name, target.Host);
                // Explicit context so handlers (e.g. open_tab) can create additional pages.
                var context = await browser.NewContextAsync(NewContextOptions(config, acceptDownloads: false));
                var page = await context.NewPageAsync();
                try
                {
                    await RunTargetAsync(page, target, config, progress, ct);
                }
                finally
                {
                    await context.CloseAsync();
                }
            }
        }
        finally
        {
            await browser.CloseAsync();
            Log.Info("Projekt-Ausführung beendet");
        }
    }

    /// <summary>Runs a single target's flow in an existing page.</summary>
    public async Task RunTargetAsync(
        IPage page,
        TargetConfig target,
        RunConfig config,
        IProgress<TraceEntry>? progress = null,
        CancellationToken ct = default)
    {
        var actionsPath = Path.Combine(config.ProjectDir, target.ActionsFile);
        var doc = await FlowSerializer2.LoadAsync(actionsPath);
        var mainTab = MainTab(doc);
        var ctx = CreateContext(page, target, config, config.ProjectDir, doc, progress, ct);
        await ExecuteWiredAsync(doc, mainTab.Id, ctx);
    }

    /// <summary>
    /// Runs an in-memory document directly (no project/targets), opening its own browser.
    /// Used to execute the flow currently open in the editor against a default target.
    /// </summary>
    public async Task RunDocumentAsync(
        FlowDocument2 doc,
        RunConfig config,
        TargetConfig target,
        IProgress<TraceEntry>? progress = null,
        CancellationToken ct = default,
        Func<string, Task>? onPause = null,
        Func<FlowNode, Task>? pauseGate = null,
        Func<AiRequest, CancellationToken, Task<string>>? aiComplete = null,
        Func<string, string, string?>? secretLookup = null)
    {
        Log.Info("Dokument-Ausführung gestartet: {0} Nodes, Browser={1}", doc.Nodes.Count, config.Browser);
        // Dokument zuerst prüfen: einen Browser hochzufahren, nur um danach an einer kaputten
        // Datei zu scheitern, kostet Sekunden und hinterlässt einen halben Playwright-Start.
        var mainTab = MainTab(doc);
        ApplyDriverPath(config);
        using var playwright = await Playwright.CreateAsync();
        var browser = await LaunchBrowserAsync(playwright, config);
        try
        {
            // Explicit context so handlers (e.g. open_tab) can create additional pages.
            var context = await browser.NewContextAsync(NewContextOptions(config, acceptDownloads: true));
            var page = await context.NewPageAsync();

            // Browser-Downloads mit echtem Namen im Zielordner speichern (statt GUID-Temp).
            // An JEDE Seite hängen — auch an Popups/neue Tabs, die die Seite selbst öffnet
            // (z. B. pixeldrain) und die nicht über den open_tab-Node laufen.
            var downloads = new DownloadCollector(ResolveDownloadDir(config));
            context.Page += (_, p) =>
            {
                Log.Debug("Neue Seite im Kontext: {0}", p.Url);
                downloads.Attach(p);
            };
            downloads.Attach(page);

            try
            {
                var ctx = CreateContext(page, target, config, config.ProjectDir, doc, progress, ct, onPause, pauseGate, downloads.Attach, downloads.Save, aiComplete, secretLookup);
                await ExecuteWiredAsync(doc, mainTab.Id, ctx);
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                    await downloads.WaitAllAsync(); // laufende Downloads vor dem Schließen fertigstellen
                await context.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
            Log.Info("Dokument-Ausführung beendet");
        }
    }

    /// <summary>
    /// Runs a single tab against an already-created page (or null for browser-free flows).
    /// Wires up the same execution context as a normal run; primarily a testability hook.
    /// </summary>
    public Task RunTabAsync(FlowDocument2 doc, string tabId, IPage page, RunConfig config,
        TargetConfig target, IProgress<TraceEntry>? progress = null, CancellationToken ct = default)
    {
        var ctx = CreateContext(page, target, config, config.ProjectDir, doc, progress, ct);
        return ExecuteWiredAsync(doc, tabId, ctx);
    }

    /// <summary>Control nodes route their own outputs (via ctx.FollowOutput) instead of auto-following.</summary>
    private static bool IsControlNode(string type) =>
        type is "if_then_else" or "foreach" or "for_range" or "get_links" or "use_session"
        || NodeCatalog.Get(type)?.RoutesOutputs == true; // Plugins mit eigenen Ausgängen

    /// <summary>
    /// Executes a tab as a wired graph: starts from entry nodes (no incoming wires)
    /// and follows wires. Branches/loops route via output ports.
    /// </summary>
    public async Task ExecuteWiredAsync(FlowDocument2 doc, string tabId, ExecutionContext ctx)
    {
        var tabNodes = doc.Nodes.Where(n => n.TabId == tabId && !IsAnnotation(n.Type)).ToList();
        if (tabNodes.Count == 0) return;

        var incoming = doc.BuildIncomingSet(tabId);
        var entryIds = tabNodes.Where(n => !incoming.Contains(n.Id)).Select(n => n.Id).ToList();
        await TraverseFrom(entryIds, ctx);
    }

    /// <summary>Traverses the given target nodes with a fresh visited set.</summary>
    private async Task TraverseFrom(IEnumerable<string> ids, ExecutionContext ctx)
    {
        var visited = new HashSet<string>();
        foreach (var id in ids)
        {
            var node = ctx.Document?.GetNode(id);
            if (node is not null && !IsAnnotation(node.Type))
                await RunNode(node, visited, ctx);
        }
    }

    private async Task RunNode(FlowNode node, HashSet<string> visited, ExecutionContext ctx)
    {
        if (!visited.Add(node.Id)) return;
        ctx.CancellationToken.ThrowIfCancellationRequested();
        await ctx.CheckPauseAsync(node); // manuelles Pausieren: hält vor dem Node
        ctx.CancellationToken.ThrowIfCancellationRequested();

        Log.Info("Node startet: {0} ({1})", node.Type, node.Id);
        ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Running,
            DateTime.Now, ctx.Target.Name, NoSnapshot));

        var handler = _registry.Get(node.Type);
        if (handler is null)
        {
            Log.Warn("Unbekannter Action-Typ: {0} ({1})", node.Type, node.Id);
            ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Skipped,
                DateTime.Now, ctx.Target.Name, NoSnapshot,
                $"Unbekannter Action-Typ: {node.Type}"));
        }
        else
        {
            // Optionale Wiederholversuche pro Node (Config "retry" = Anzahl, "retry_delay_ms" = Pause).
            // Praktisch bei flaky Seiten/Netz; 0 = wie bisher (ein Versuch). Quit/Abbruch nie wiederholen.
            var maxRetries = int.TryParse(node.Get("retry"), out var r) && r > 0 ? r : 0;
            var retryDelayMs = int.TryParse(node.Get("retry_delay_ms"), out var d) && d > 0 ? d : 0;

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await handler.ExecuteAsync(ctx, node);
                    Log.Debug("Node erfolgreich: {0} ({1})", node.Type, node.Id);
                    ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                        DateTime.Now, ctx.Target.Name, NoSnapshot));
                    break;
                }
                catch (QuitException)
                {
                    Log.Info("Quit: {0} ({1})", node.Type, node.Id);
                    ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                        DateTime.Now, ctx.Target.Name, NoSnapshot, "Quit"));
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    Log.Warn("Node fehlgeschlagen (Versuch {0}/{1}): {2} ({3}): {4} — wird wiederholt",
                        attempt + 1, maxRetries + 1, node.Type, node.Id, ex.Message);
                    ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Running,
                        DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                        $"Fehler – Versuch {attempt + 1}/{maxRetries + 1}: {ex.Message}"));
                    if (retryDelayMs > 0) await Task.Delay(retryDelayMs, ctx.CancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Node fehlgeschlagen: {0} ({1}): {2}", node.Type, node.Id, ex.Message);
                    ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Error,
                        DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                        ErrorMessage: ex.Message));
                    return; // stop this path on error
                }
            }
        }

        // Control nodes already routed via ctx.FollowOutput inside their handler.
        // Normal nodes auto-follow their first output port within the same traversal.
        if (IsControlNode(node.Type)) return;
        if (node.Wires.Count > 0)
        {
            foreach (var targetId in node.Wires[0])
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var next = ctx.Document?.GetNode(targetId);
                if (next is not null && !IsAnnotation(next.Type))
                    await RunNode(next, visited, ctx);
            }
        }
    }

    private ExecutionContext CreateContext(
        IPage page, TargetConfig target, RunConfig config,
        string projectDir, FlowDocument2 doc,
        IProgress<TraceEntry>? progress, CancellationToken ct,
        Func<string, Task>? onPause = null, Func<FlowNode, Task>? pauseGate = null,
        Action<IPage>? attachDownloads = null, Func<IDownload, Task>? saveDownload = null,
        Func<AiRequest, CancellationToken, Task<string>>? aiComplete = null,
        Func<string, string, string?>? secretLookup = null)
    {
        return new ExecutionContext(page, target, config, projectDir,
            progress: progress, cancellationToken: ct)
        {
            Document = doc,
            AttachDownloads = attachDownloads,
            SaveDownload = saveDownload,
            AiComplete = aiComplete,
            SecretLookup = secretLookup,
            // call → run a named subnode's tab as a fresh wired traversal.
            RunSubTabCallback = (tabId, c) => c.Document is null
                ? Task.CompletedTask
                : ExecuteWiredAsync(c.Document, tabId, c),
            // if/foreach → route a specific output port (fresh traversal so loop bodies re-run).
            FollowOutputCallback = (node, port, c) =>
                TraverseFrom(node.Wires.ElementAtOrDefault(port) ?? [], c),
            PauseCallback = onPause,
            PauseGate = pauseGate,
        };
    }

    /// <summary>
    /// Der Haupt-Tab des Dokuments (der einzige, der kein Unterflow ist).
    ///
    /// Eine Datei ohne Haupt-Tab ist von Hand verbogen oder halb geschrieben worden. Vorher endete
    /// das in einer nackten InvalidOperationException aus LINQ ("Sequence contains no matching
    /// element"), die nichts darüber sagt, welche Datei gemeint ist.
    /// </summary>
    private static FlowTab MainTab(FlowDocument2 doc) =>
        doc.Tabs.FirstOrDefault(t => !t.IsSubFlow)
        ?? throw new InvalidOperationException(
            "Der Flow hat keinen Haupt-Tab (alle Tabs sind als Unterflow markiert) — "
            + "die Datei ist unvollständig oder beschädigt.");

    private static bool IsAnnotation(string type) =>
        type is "label" or "caption" or "note";

    /// <summary>Zielordner für Downloads: konfiguriert, sonst der Standard-Downloadordner des Nutzers.</summary>
    private static string ResolveDownloadDir(RunConfig config) =>
        string.IsNullOrWhiteSpace(config.DownloadDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            : config.DownloadDir;

    /// <summary>Sucht die Brave-Programmdatei an den üblichen Orten (OS-abhängig).</summary>
    private static string? FindBraveExecutable()
    {
        string[] candidates;
        if (OperatingSystem.IsWindows())
        {
            string[] roots =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ];
            candidates = roots
                .Where(r => !string.IsNullOrEmpty(r))
                .Select(r => Path.Combine(r, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"))
                .ToArray();
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates = ["/Applications/Brave Browser.app/Contents/MacOS/Brave Browser"];
        }
        else
        {
            candidates =
            [
                "/usr/bin/brave-browser", "/usr/bin/brave-browser-stable", "/usr/bin/brave",
                "/opt/brave.com/brave/brave-browser", "/opt/brave.com/brave/brave", "/snap/bin/brave",
            ];
        }

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null) Log.Info("Brave gefunden: {0}", found);
        return found;
    }

    /// <summary>If a driver path is configured, point Playwright at it (used when the
    /// driver can't be located automatically).</summary>
    private static void ApplyDriverPath(RunConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.DriverPath)) return;
        Log.Info("Playwright-Treiberpfad: {0}", config.DriverPath);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_PATH", config.DriverPath);
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright pw, RunConfig config)
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = config.Headless,
            SlowMo = config.SlowMoMs,
            // Kein fester DownloadsPath: Playwright nutzt seinen verwalteten Temp; die finale
            // Datei schreibt der DownloadCollector per SaveAsAsync mit echtem Namen ins Ziel.
        };

        // Maximiert starten: nur Chromium versteht --start-maximized; zusammen mit dem
        // deaktivierten Viewport (siehe NewContextOptions) füllt die Seite das ganze Fenster.
        var launchArgs = MaximizeArgs(config);
        if (launchArgs.Count > 0)
        {
            options.Args = launchArgs;
            Log.Info("Browser-Fenster maximiert (--start-maximized)");
        }

        if (config.BrowserChannel.Equals("brave", StringComparison.OrdinalIgnoreCase))
        {
            // Brave ist kein Playwright-Channel → als Chromium mit der Brave-Programmdatei starten.
            var exe = string.IsNullOrWhiteSpace(config.BrowserExecutablePath)
                ? FindBraveExecutable()
                : config.BrowserExecutablePath;
            if (!string.IsNullOrEmpty(exe))
                options.ExecutablePath = exe;
            else
                Log.Warn("Brave nicht gefunden — bitte Programmpfad in den Einstellungen angeben.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(config.BrowserChannel))
                options.Channel = config.BrowserChannel;
            if (!string.IsNullOrWhiteSpace(config.BrowserExecutablePath))
                options.ExecutablePath = config.BrowserExecutablePath;
        }
        if (!string.IsNullOrWhiteSpace(config.ProxyServer))
        {
            options.Proxy = new Proxy
            {
                Server = config.ProxyServer,
                Bypass = string.IsNullOrWhiteSpace(config.ProxyBypass) ? null : config.ProxyBypass,
                Username = string.IsNullOrWhiteSpace(config.ProxyUsername) ? null : config.ProxyUsername,
                Password = string.IsNullOrWhiteSpace(config.ProxyPassword) ? null : config.ProxyPassword,
            };
            Log.Info("Proxy aktiv: {0}", config.ProxyServer);
        }

        Log.Info("Browser starten: {0}{1}{2}", config.Browser,
            string.IsNullOrWhiteSpace(config.BrowserChannel) ? "" : $" (Channel {config.BrowserChannel})",
            string.IsNullOrWhiteSpace(config.BrowserExecutablePath) ? "" : $" @ {config.BrowserExecutablePath}");

        return config.Browser.ToLowerInvariant() switch
        {
            "firefox" => await pw.Firefox.LaunchAsync(options),
            "webkit" => await pw.Webkit.LaunchAsync(options),
            _ => await pw.Chromium.LaunchAsync(options),
        };
    }

    /// <summary>True für Chromium-basierte Browser (Chromium/Chrome/Edge/Brave laufen über den
    /// Chromium-Launcher) — Firefox und WebKit kennen <c>--start-maximized</c> nicht.</summary>
    private static bool IsChromiumBased(RunConfig config) =>
        config.Browser.Trim().ToLowerInvariant() is not "firefox" and not "webkit";

    /// <summary>Chromium-Startargumente, um das sichtbare Fenster maximiert zu öffnen.</summary>
    public static IReadOnlyList<string> MaximizeArgs(RunConfig config) =>
        config.Maximized && !config.Headless && IsChromiumBased(config)
            ? ["--start-maximized"]
            : [];

    /// <summary>Bei maximiertem, sichtbarem Fenster den festen Standard-Viewport (1280×720)
    /// deaktivieren, damit die Seite die volle Fenstergröße nutzt.</summary>
    public static bool UseWindowViewport(RunConfig config) =>
        config.Maximized && !config.Headless;

    /// <summary>Erzeugt die Kontext-Optionen (Downloads, ggf. fenstergroßer Viewport, ggf. Sitzung).</summary>
    private static BrowserNewContextOptions NewContextOptions(RunConfig config, bool acceptDownloads)
    {
        var options = new BrowserNewContextOptions { AcceptDownloads = acceptDownloads };
        if (UseWindowViewport(config))
            options.ViewportSize = ViewportSize.NoViewport;

        // Gespeicherte Sitzung (Cookies + localStorage) laden → Login/Captcha entfällt.
        var session = ResolveSessionPath(config);
        if (session is not null && File.Exists(session))
        {
            options.StorageStatePath = session;
            Log.Info("Sitzung geladen: {0}", session);
        }
        return options;
    }

    /// <summary>Pfad zur Sitzungsdatei, wenn Wiederverwendung aktiv ist — sonst null.</summary>
    public static string? ResolveSessionPath(RunConfig config)
    {
        if (!config.SessionPersist) return null;
        var path = config.SessionFile;
        if (string.IsNullOrWhiteSpace(path))
            return Path.Combine(string.IsNullOrEmpty(config.ProjectDir) ? "." : config.ProjectDir, "session.json");
        return Path.IsPathRooted(path) ? path : Path.Combine(config.ProjectDir, path);
    }
}

/// <summary>Thrown by the quit action to stop the current target's execution.</summary>
public sealed class QuitException : Exception
{
    public QuitException() : base("Quit action executed") { }
}
