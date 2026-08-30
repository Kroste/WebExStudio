using WebExStudio.Engine;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// Muster kommen aus dem Flow, Eingaben von der besuchten Seite — beides unkontrolliert. Ohne
/// Zeitlimit hängt ein katastrophal backtrackendes Muster den Lauf endlos auf, ohne Fehler und
/// ohne dass „Stoppen" noch greift.
/// </summary>
public class SafeRegexTests
{
    [Fact]
    public void NormaleMuster_FunktionierenWieGewohnt()
    {
        Assert.True(SafeRegex.IsMatch("Preis: 42 Euro", @"\d+"));
        Assert.False(SafeRegex.IsMatch("kein Treffer", @"\d+"));
        Assert.Equal("42", SafeRegex.Match("Preis: 42 Euro", @"\d+").Value);
    }

    [Fact]
    public void KatastrophalesBacktracking_LaeuftInsZeitlimit()
    {
        // Klassiker: (a+)+b auf einer Zeile aus lauter 'a' ohne abschließendes 'b'.
        var eingabe = new string('a', 40);

        var ex = Assert.Throws<InvalidOperationException>(() => SafeRegex.IsMatch(eingabe, "(a+)+b"));

        Assert.Contains("Zeitlimit", ex.Message);
        Assert.Contains("(a+)+b", ex.Message); // Muster steht in der Meldung, sonst sucht man lange
    }

    [Fact]
    public void UngueltigesMuster_LiefertKlareMeldung()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SafeRegex.IsMatch("x", "([unfertig"));
        Assert.Contains("Ungültiger regulärer Ausdruck", ex.Message);
    }
}
