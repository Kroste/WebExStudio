using System.Text.RegularExpressions;

namespace WebExStudio.Engine;

/// <summary>
/// Reguläre Ausdrücke aus Flows — mit Zeitlimit und verständlichen Fehlern.
///
/// WARUM: Muster kommen aus der Node-Konfiguration, die Eingabe von der besuchten Seite. Beide
/// sind unkontrolliert. Ein Muster mit verschachtelten Quantoren (klassisch <c>(a+)+b</c>) braucht
/// auf einem langen Seitentext exponentiell lange — die .NET-Regex-Engine läuft dann ohne Zeitlimit
/// endlos, und der Lauf hängt mitten im Flow fest, ohne Fehlermeldung und ohne dass "Stoppen" noch
/// greift (der Abbruch wird erst zwischen den Nodes geprüft).
///
/// Zwei Sekunden sind für jedes vernünftige Muster reichlich; wer sie reißt, hat ein Problem im
/// Muster und keine langsame Seite. Der Fehler wird als klare Meldung geworfen und landet damit als
/// Node-Fehler im Protokoll — statt als stiller Hänger.
/// </summary>
public static class SafeRegex
{
    /// <summary>Zeitlimit je Auswertung.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static bool IsMatch(string input, string pattern) =>
        Run(() => Regex.IsMatch(input, pattern, RegexOptions.None, Timeout), pattern);

    public static Match Match(string input, string pattern) =>
        Run(() => Regex.Match(input, pattern, RegexOptions.None, Timeout), pattern);

    private static T Run<T>(Func<T> auswertung, string pattern)
    {
        try
        {
            return auswertung();
        }
        catch (RegexMatchTimeoutException)
        {
            throw new InvalidOperationException(
                $"Der reguläre Ausdruck '{pattern}' hat das Zeitlimit von {Timeout.TotalSeconds:0} Sekunden "
                + "überschritten (vermutlich katastrophales Backtracking). Muster vereinfachen.");
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Ungültiger regulärer Ausdruck '{pattern}': {ex.Message}");
        }
    }
}
