using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

public class ActionRegistryTests
{
    [Theory]
    [InlineData("goto")]
    [InlineData("open_tab")]
    [InlineData("close_tab")]
    [InlineData("get_links")]
    [InlineData("click")]
    [InlineData("send_keys")]
    [InlineData("wait_for")]
    [InlineData("sleep")]
    [InlineData("menu_path")]
    [InlineData("scroll")]
    [InlineData("press_key")]
    [InlineData("select_option")]
    [InlineData("hover")]
    [InlineData("if_then_else")]
    [InlineData("for_range")]
    [InlineData("foreach")]
    [InlineData("call")]
    [InlineData("noop")]
    [InlineData("quit")]
    [InlineData("assert")]
    [InlineData("screenshot")]
    [InlineData("eval_js")]
    [InlineData("save_session")]
    [InlineData("ai_query")]
    [InlineData("download_stream")]
    [InlineData("page_function")]
    [InlineData("get_value")]
    [InlineData("set_payload")]
    [InlineData("debug")]
    [InlineData("function")]
    [InlineData("read_file")]
    [InlineData("write_file")]
    [InlineData("download_url")]
    [InlineData("captcha_guard")]
    [InlineData("label")]
    [InlineData("caption")]
    [InlineData("navigate_to")] // alias for goto
    public void CreateDefault_RegistersHandler(string type)
    {
        var registry = ActionRegistry.CreateDefault();
        Assert.NotNull(registry.Get(type));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var registry = ActionRegistry.CreateDefault();
        Assert.NotNull(registry.Get("GOTO"));
        Assert.Null(registry.Get("unknown_type"));
    }
}
