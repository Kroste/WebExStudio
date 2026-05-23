using Microsoft.Playwright;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine;

public sealed class FlowExecutor
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
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
                var context = await browser.NewContextAsync();
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
        var mainTab = doc.Tabs.First(t => !t.IsSubFlow);
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
        Func<string, Task>? onPause = null)
    {
        Log.Info("Dokument-Ausführung gestartet: {0} Nodes, Browser={1}", doc.Nodes.Count, config.Browser);
        ApplyDriverPath(config);
        using var playwright = await Playwright.CreateAsync();
        var browser = await LaunchBrowserAsync(playwright, config);
        try
        {
            // Explicit context so handlers (e.g. open_tab) can create additional pages.
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            try
            {
                var mainTab = doc.Tabs.First(t => !t.IsSubFlow);
                var ctx = CreateContext(page, target, config, config.ProjectDir, doc, progress, ct, onPause);
                await ExecuteWiredAsync(doc, mainTab.Id, ctx);
            }
            finally
            {
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
    /// Executes nodes on a wired (main canvas) tab in topological order.
    /// Entry nodes are those with no incoming wires.
    /// </summary>
    public async Task ExecuteWiredAsync(FlowDocument2 doc, string tabId, ExecutionContext ctx)
    {
        // Annotation nodes (label/caption) are visual-only — never execute them.
        var tabNodes = doc.Nodes.Where(n => n.TabId == tabId && !IsAnnotation(n.Type)).ToList();
        if (tabNodes.Count == 0) return;

        var incoming = doc.BuildIncomingSet(tabId);
        var entryNodes = tabNodes.Where(n => !incoming.Contains(n.Id)).ToList();

        // Build adjacency for topological traversal
        var nodeById = tabNodes.ToDictionary(n => n.Id);
        var visited = new HashSet<string>();

        foreach (var entry in entryNodes)
            await TraverseAsync(doc, entry, nodeById, visited, ctx);
    }

    private async Task TraverseAsync(
        FlowDocument2 doc, FlowNode node,
        Dictionary<string, FlowNode> nodeById,
        HashSet<string> visited,
        ExecutionContext ctx)
    {
        if (!visited.Add(node.Id)) return;
        ctx.CancellationToken.ThrowIfCancellationRequested();

        Log.Info("Node startet: {0} ({1})", node.Type, node.Id);
        ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Running,
            DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));

        var handler = _registry.Get(node.Type);
        if (handler is null)
        {
            Log.Warn("Unbekannter Action-Typ: {0} ({1})", node.Type, node.Id);
            ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Skipped,
                DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                $"Unbekannter Action-Typ: {node.Type}"));
        }
        else
        {
            try
            {
                await handler.ExecuteAsync(ctx, node);
                Log.Debug("Node erfolgreich: {0} ({1})", node.Type, node.Id);
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));
            }
            catch (QuitException)
            {
                Log.Info("Quit: {0} ({1})", node.Type, node.Id);
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(), "Quit"));
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Node fehlgeschlagen: {0} ({1}): {2}", node.Type, node.Id, ex.Message);
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Error,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                    ErrorMessage: ex.Message));
                return; // stop traversal on error
            }
        }

        // Follow output wires sequentially
        foreach (var port in node.Wires)
        {
            foreach (var targetId in port)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                if (nodeById.TryGetValue(targetId, out var next))
                    await TraverseAsync(doc, next, nodeById, visited, ctx);
            }
        }
    }

    private ExecutionContext CreateContext(
        IPage page, TargetConfig target, RunConfig config,
        string projectDir, FlowDocument2 doc,
        IProgress<TraceEntry>? progress, CancellationToken ct,
        Func<string, Task>? onPause = null)
    {
        return new ExecutionContext(page, target, config, projectDir,
            progress: progress, cancellationToken: ct)
        {
            Document = doc,
            // Every tab (main, subnodes, branch tabs) executes by following wires.
            RunSubTabCallback = (tabId, c) => c.Document is null
                ? Task.CompletedTask
                : ExecuteWiredAsync(c.Document, tabId, c),
            PauseCallback = onPause,
        };
    }

    private static bool IsAnnotation(string type) =>
        type is "label" or "caption";

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
            DownloadsPath = string.IsNullOrEmpty(config.DownloadDir) ? null : config.DownloadDir,
        };

        if (!string.IsNullOrWhiteSpace(config.BrowserChannel))
            options.Channel = config.BrowserChannel;
        if (!string.IsNullOrWhiteSpace(config.BrowserExecutablePath))
            options.ExecutablePath = config.BrowserExecutablePath;

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
}

/// <summary>Thrown by the quit action to stop the current target's execution.</summary>
public sealed class QuitException : Exception
{
    public QuitException() : base("Quit action executed") { }
}
