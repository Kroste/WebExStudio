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
        var flow = await FlowSerializer.LoadAsync(actionsPath);
        var ctx = CreateContext(page, target, config, config.ProjectDir, progress, ct);
        await ExecuteActionsAsync(ctx, flow.Actions);
    }

    /// <summary>Executes an action list — the inner loop used recursively by control-flow handlers.</summary>
    public async Task ExecuteActionsAsync(ExecutionContext ctx, List<ActionNode> actions)
    {
        foreach (var node in actions)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var nodeId = node.Ui?.Id ?? node.Type;
            ctx.Report(new TraceEntry(nodeId, node.Type, ExecutionStatus.Running,
                DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));

            var handler = _registry.Get(node.Type);
            if (handler is null)
            {
                ctx.Report(new TraceEntry(nodeId, node.Type, ExecutionStatus.Skipped,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                    $"Unbekannter Action-Typ: {node.Type}"));
                continue;
            }

            try
            {
                await handler.ExecuteAsync(ctx, node);
                ctx.Report(new TraceEntry(nodeId, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot()));
            }
            catch (QuitException)
            {
                ctx.Report(new TraceEntry(nodeId, node.Type, ExecutionStatus.Success,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(), "Quit"));
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ctx.Report(new TraceEntry(nodeId, node.Type, ExecutionStatus.Error,
                    DateTime.Now, ctx.Target.Name, ctx.ContextSnapshot(),
                    ErrorMessage: ex.Message));
            }
        }
    }

    private ExecutionContext CreateContext(
        IPage page, TargetConfig target, RunConfig config,
        string projectDir, IProgress<TraceEntry>? progress, CancellationToken ct)
    {
        return new ExecutionContext(page, target, config, projectDir,
            progress: progress, cancellationToken: ct)
        {
            RunSubActionsCallback = ExecuteActionsAsync
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
