using WebExStudio.Core.Localization;
using Xunit;

namespace WebExStudio.Core.Tests;

/// <summary>Sprachdienst: eingebettete Wörterbücher, Umschaltung, Fallback auf den Schlüssel.</summary>
public class LocTests
{
    [Fact]
    public void ListsEmbeddedLanguages_WithNames()
    {
        var loc = Loc.Instance;
        Assert.Contains("de", loc.Languages);
        Assert.Contains("en", loc.Languages);
        Assert.Equal("Deutsch", loc.NameOf("de"));
        Assert.Equal("English", loc.NameOf("en"));
    }

    [Fact]
    public void Switches_BetweenGermanAndEnglish()
    {
        var loc = Loc.Instance;
        try
        {
            loc.SetLanguage("de");
            Assert.Equal("Einstellungen", loc["Tip_Settings"]);
            loc.SetLanguage("en");
            Assert.Equal("Settings", loc["Tip_Settings"]);
        }
        finally
        {
            loc.SetLanguage("de"); // Singleton wieder zurücksetzen
        }
    }

    [Fact]
    public void UnknownKey_FallsBackToKey()
    {
        Assert.Equal("Kein_Solcher_Schluessel_42", Loc.Instance["Kein_Solcher_Schluessel_42"]);
    }
}
