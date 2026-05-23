using Microsoft.Playwright;
using WebExStudio.Core.Models;
using WebExStudio.Core.Serialization;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine;

public sealed class FlowExecutor
{
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
        using var playwright = await Playwright.CreateAsync();
        var browser = await LaunchBrowserAsync(playwright, config);
        try
        {
            foreach (var target in targets.Where(t => t.Enabled))
            {
                ct.ThrowIfCancellationRequested();
                var page = await browser.NewPageAsync();
                try
                {
                    await RunTargetAsync(page, target, config, progress, ct);
                }
                finally
                {
                    await page.CloseAsync();
                }
            }
        }
        finally
        {
            await browser.CloseAsync();
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
        CancellationToken ct = default)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await LaunchBrowserAsync(playwright, config);
        try
        {
            var page = await browser.NewPageAsync();
            try
            {
                var mainTab = doc.Tabs.First(t => !t.IsSubFlow);
                var ctx = CreateContext(page, target, config, config.ProjectDir, doc, progress, ct);
                await ExecuteWiredAsync(doc, mainTab.Id, ctx);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    /// <summary>
    /// Executes nodes on a wired (main canvas) tab in topological order.
    /// Entry nodes are those with no incoming wires.
    /// </summary>
    public async Task ExecuteWiredAsync(FlowDocument2 doc, string tabId, ExecutionContext ctx)
    {
        var tabNodes = doc.Nodes.Where(n => n.TabId == tabId).ToList();
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

        ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Running,
            DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));

        var handler = _registry.Get(node.Type);
        if (handler is null)
        {
            ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Skipped,
                DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                $"Unbekannter Action-Typ: {node.Type}"));
        }
        else
        {
            try
            {
                await handler.ExecuteAsync(ctx, node);
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));
            }
            catch (QuitException)
            {
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

    /// <summary>
    /// Executes nodes on a sequential (sub-flow) tab in seqIndex order.
    /// Used by control-flow handlers (if/else branches, loop bodies, call tabs).
    /// </summary>
    public async Task ExecuteSequentialAsync(string tabId, ExecutionContext ctx)
    {
        if (ctx.Document is null) return;
        var nodes = ctx.Document.GetNodes(tabId).ToList();
        foreach (var node in nodes)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Running,
                DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));

            var handler = _registry.Get(node.Type);
            if (handler is null)
            {
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Skipped,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                    $"Unbekannter Action-Typ: {node.Type}"));
                continue;
            }

            try
            {
                await handler.ExecuteAsync(ctx, node);
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));
            }
            catch (QuitException)
            {
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
                ctx.Report(new TraceEntry(node.Id, node.Type, ExecutionStatus.Error,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                    ErrorMessage: ex.Message));
            }
        }
    }

    private ExecutionContext CreateContext(
        IPage page, TargetConfig target, RunConfig config,
        string projectDir, FlowDocument2 doc,
        IProgress<TraceEntry>? progress, CancellationToken ct)
    {
        return new ExecutionContext(page, target, config, projectDir,
            progress: progress, cancellationToken: ct)
        {
            Document = doc,
            RunSubTabCallback = ExecuteSequentialAsync,
        };
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright pw, RunConfig config)
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = config.Headless,
            SlowMo = config.SlowMoMs,
            DownloadsPath = string.IsNullOrEmpty(config.DownloadDir) ? null : config.DownloadDir,
        };

        return config.Browser.ToLowerInvariant() switch
        {
            "firefox" => await pw.Firefox.LaunchAsync(options),
            _ => await pw.Chromium.LaunchAsync(options),
        };
    }
}

/// <summary>Thrown by the quit action to stop the current target's execution.</summary>
public sealed class QuitException : Exception
{
    public QuitException() : base("Quit action executed") { }
}
