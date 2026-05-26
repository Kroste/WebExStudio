using WebExStudio.Core.Models;

namespace WebExStudio.Engine.Actions;

public sealed class ActionRegistry
{
    private readonly Dictionary<string, IActionHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public ActionRegistry Register(IActionHandler handler)
    {
        _handlers[handler.Type] = handler;
        return this;
    }

    public IActionHandler? Get(string type) =>
        _handlers.GetValueOrDefault(type);

    public IReadOnlyCollection<IActionHandler> All => _handlers.Values;

    public static ActionRegistry CreateDefault()
    {
        var r = new ActionRegistry();
        r.Register(new GotoHandler());
        r.Register(new OpenTabHandler());
        r.Register(new CloseTabHandler());
        r.Register(new GetLinksHandler());
        r.Register(new ClickHandler());
        r.Register(new SendKeysHandler());
        r.Register(new WaitForHandler());
        r.Register(new SleepHandler());
        r.Register(new MenuPathHandler());
        r.Register(new ScrollHandler());
        r.Register(new PressKeyHandler());
        r.Register(new SelectOptionHandler());
        r.Register(new HoverHandler());
        r.Register(new IfThenElseHandler());
        r.Register(new ForRangeHandler());
        r.Register(new ForeachHandler());
        r.Register(new CallHandler());
        r.Register(new NoopHandler());
        r.Register(new QuitHandler());
        r.Register(new AssertHandler());
        r.Register(new GetValueHandler());
        r.Register(new SetPayloadHandler());
        r.Register(new DebugHandler());
        r.Register(new FunctionHandler());
        r.Register(new ReadFileHandler());
        r.Register(new WriteFileHandler());
        r.Register(new DownloadUrlHandler());
        r.Register(new ScreenshotHandler());
        r.Register(new DownloadStreamHandler());
        r.Register(new PageFunctionHandler());
        r.Register(new SaveSessionHandler());
        r.Register(new UseSessionHandler());
        r.Register(new AiQueryHandler());
        r.Register(new CaptchaGuardHandler());
        r.Register(new LabelHandler());
        r.Register(new CaptionHandler());
        r.Register(new NoteHandler());
        // navigate_to is an alias for goto
        r.Register(new AliasHandler("navigate_to", r.Get("goto")!));
        // eval_js wurde in den Function-Node (page_function) zusammengeführt — Alias für Altbestand.
        r.Register(new AliasHandler("eval_js", r.Get("page_function")!));
        return r;
    }
}

/// <summary>Forwards one type-name to another handler.</summary>
internal sealed class AliasHandler(string type, IActionHandler target) : IActionHandler
{
    public string Type => type;
    public Task ExecuteAsync(ExecutionContext ctx, FlowNode node) =>
        target.ExecuteAsync(ctx, node);
}
