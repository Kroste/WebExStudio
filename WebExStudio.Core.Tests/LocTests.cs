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

    [Fact]
    public void AllLanguages_HaveNameAndFlag()
    {
        var loc = Loc.Instance;
        foreach (var lang in loc.Languages)
        {
            Assert.False(string.IsNullOrWhiteSpace(loc.NameOf(lang)), $"{lang} ohne @name");
            Assert.False(string.IsNullOrWhiteSpace(loc.FlagOf(lang)), $"{lang} ohne @flag");
        }
    }

    /// <summary>
    /// Englisch ist die vollständige Referenz (Chrome- UND Node-Schlüssel). Jede andere übersetzte
    /// Sprache (fr, ru …) muss jeden en-Schlüssel kennen — fehlt einer, erschiene dort der Roh-Key
    /// bzw. (bei Node-Texten) Deutsch. Deutsch ist ausgenommen (Node-Texte = Literale im Katalog).
    /// </summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("ru")]
    public void Translation_HasEveryEnglishKey(string lang)
    {
        var loc = Loc.Instance;
        var missing = loc.Keys("en").Where(k => k != "@name" && k != "@flag" && !loc.Has(lang, k)).ToList();
        Assert.True(missing.Count == 0, $"In {lang}.json fehlen: " + string.Join(", ", missing));
    }
}
