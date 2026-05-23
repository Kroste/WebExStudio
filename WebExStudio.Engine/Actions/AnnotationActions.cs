using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

/// <summary>Visual-only annotation node: displays comment text, does nothing on execution.</summary>
public sealed class LabelHandler : IActionHandler
{
    public string Type => "label";
    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => Task.CompletedTask;
}

/// <summary>Visual-only annotation node: displays a heading, does nothing on execution.</summary>
public sealed class CaptionHandler : IActionHandler
{
    public string Type => "caption";
    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) => Task.CompletedTask;
}
