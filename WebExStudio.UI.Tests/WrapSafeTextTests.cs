using WebExStudio.UI.Text;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Fixiert das Auslöser-Rezept des Avalonia-12.1.1-Layout-Hängers (siehe <see cref="WrapSafeText"/>):
/// StackPanel + TextWrapping.Wrap + leere Zeile ⇒ Endlos-Layout, unbegrenzter Speicherverbrauch.
///
/// Der Live-Beweis, dass die Entschärfung greift, sind die Fenster-Smoke-Tests: das Hilfefenster
/// rendert die README, deren JSON-Beispiele genau solche Leerzeilen enthalten. Hier steht die
/// reine Textlogik — ohne Layout, damit ein Fehler eine klare Meldung liefert statt eines Hängers.
/// </summary>
public class WrapSafeTextTests
{
    [Fact]
    public void LeereZeile_WirdZuLeerzeichen()
    {
        Assert.Equal("abc\n \nabc", WrapSafeText.Sanitize("abc\n\nabc"));
    }

    [Theory]
    [InlineData("\n\nabc", " \n \nabc")]          // führende Leerzeilen
    [InlineData("abc\n\n", "abc\n \n ")]          // abschließende Leerzeilen
    [InlineData("a\n\n\nb", "a\n \n \nb")]        // mehrere am Stück
    public void LeerzeilenAnJederPosition_WerdenEntschaerft(string ein, string aus)
    {
        Assert.Equal(aus, WrapSafeText.Sanitize(ein));
    }

    [Fact]
    public void UnsichtbareZeichen_ZaehlenAlsLeer()
    {
        // Zero-Width-Space hat Vorschubbreite 0 — als "Füllung" untauglich, deshalb ersetzen.
        Assert.Equal("a\n \nb", WrapSafeText.Sanitize("a\n​\nb"));
    }

    [Fact]
    public void ZeileAusLeerzeichen_BleibtUnveraendert()
    {
        // Ein echtes Leerzeichen bricht die Schleife bereits — nicht anfassen.
        Assert.Equal("a\n \nb", WrapSafeText.Sanitize("a\n \nb"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("einzeilig ohne Umbruch")]
    public void OhneZeilenumbruch_UnveraendertDurchgereicht(string? ein)
    {
        Assert.Equal(ein, WrapSafeText.Sanitize(ein));
    }

    [Fact]
    public void NormalerMehrzeilerBleibtErhalten()
    {
        Assert.Equal("a\nb\nc", WrapSafeText.Sanitize("a\nb\nc"));
    }
}
