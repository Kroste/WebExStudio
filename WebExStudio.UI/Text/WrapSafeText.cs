using System.Globalization;
using Avalonia.Data.Converters;

namespace WebExStudio.UI.Text;

/// <summary>
/// Schutz gegen einen Layout-Hänger in Avalonia 12.1.1.
///
/// SYMPTOM: Ein <c>TextBlock</c>/<c>SelectableTextBlock</c> mit
/// <c>TextWrapping="Wrap"</c>, dessen Text eine <b>leere Zeile</b> enthält
/// (<c>"…\n\nabc"</c>, auch führend oder abschließend), lässt die Layout-Schleife nie
/// konvergieren, sobald er in einem <c>StackPanel</c> steckt (also auch in jedem
/// <c>ItemsControl</c> — dessen Standard-Panel ist ein StackPanel). Der Prozess frisst
/// dann unbegrenzt Arbeitsspeicher, bis der OOM-Killer zuschlägt: keine Exception, kein
/// Log, der Rechner geht mit runter.
///
/// AUSLÖSER-REZEPT (durch <c>WrapSafeTextTests</c> und die Fenster-Smoke-Tests fixiert):
/// StackPanel + Wrap + Leerzeile. Fehlt eines der drei, passiert nichts — Border,
/// Spacing, Schriftart und Textlänge sind egal.
///
/// LÖSUNG: leere Zeilen durch ein Leerzeichen ersetzen. Optisch identisch (die Zeile
/// behält ihre Zeilenhöhe), aber der Umbruch kommt voran. Ein Zero-Width-Space (U+200B)
/// hilft NICHT — der hat Vorschubbreite 0 und die Schleife dreht weiter. Genau deshalb
/// gelten hier auch Zeilen aus lauter unsichtbaren Zeichen als leer.
/// </summary>
public static class WrapSafeText
{
    /// <summary>Ersetzt leere Zeilen durch ein Leerzeichen; alles andere bleibt unverändert.</summary>
    public static string? Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('\n')) return text;

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            if (IstUnsichtbar(lines[i])) lines[i] = " ";

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Zeile ohne jede Vorschubbreite? Nur solche Zeilen sind gefährlich — eine Zeile aus
    /// Leerzeichen ist unproblematisch und wird bewusst nicht angefasst.
    /// </summary>
    private static bool IstUnsichtbar(string line)
    {
        foreach (var ch in line)
            if (ch is not ('\r' or '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF'))
                return false;
        return true;
    }
}

/// <summary>
/// <see cref="WrapSafeText.Sanitize"/> für XAML-Bindings:
/// <c>Text="{Binding Content, Converter={x:Static text:WrapSafeTextConverter.Instance}}"</c>.
/// Nur für die Anzeige — die gebundenen Daten selbst bleiben unangetastet.
/// </summary>
public sealed class WrapSafeTextConverter : IValueConverter
{
    public static readonly WrapSafeTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        WrapSafeText.Sanitize(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("WrapSafeTextConverter ist reine Anzeige-Einbahnstraße.");
}
