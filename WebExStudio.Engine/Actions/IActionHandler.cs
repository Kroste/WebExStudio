using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public interface IActionHandler
{
    string Type { get; }
    Task ExecuteAsync(ExecutionContext ctx, FlowNode node);
}
