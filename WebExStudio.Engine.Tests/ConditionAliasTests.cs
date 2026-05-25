using WebExStudio.Engine.Actions;
using Xunit;

namespace WebExStudio.Engine.Tests;

public class ConditionAliasTests
{
    [Theory]
    [InlineData("url_contains", "page_url")]
    [InlineData("URL_CONTAINS", "page_url")]
    [InlineData("url", "page_url")]
    [InlineData("title_contains", "page_title")]
    [InlineData("text_contains", "page_contains")]
    [InlineData("body_contains", "page_contains")]
    [InlineData("element_text_contains", "element_text")]
    public void CanonicalCondition_MapsSynonyms(string input, string expected) =>
        Assert.Equal(expected, IfThenElseHandler.CanonicalCondition(input));

    [Theory]
    [InlineData("page_url")]
    [InlineData("element_exists")]
    [InlineData("payload_contains")]
    [InlineData("page_contains")]
    public void CanonicalCondition_KeepsCanonicalNames(string canonical) =>
        Assert.Equal(canonical, IfThenElseHandler.CanonicalCondition(canonical));
}
