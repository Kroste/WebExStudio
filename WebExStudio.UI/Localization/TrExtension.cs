using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using WebExStudio.Core.Localization;

namespace WebExStudio.UI.Localization;

/// <summary>
/// XAML-Markup-Extension für lokalisierte Texte: <c>{l:Tr Schlüssel}</c>.
///
/// Bindet an die normale Property <see cref="Loc.Language"/> (deren Änderung Avalonia zuverlässig
/// meldet) und schlägt den eigentlichen Text im Converter nach. So aktualisieren sich alle Texte
/// bei <see cref="Loc.SetLanguage"/> sofort (Laufzeit-Umschaltung) — Indexer-Bindungen werden von
/// Avalonia nicht zuverlässig aufgefrischt, daher dieser Umweg.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    private static readonly TrConverter Converter = new();

    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(Loc.Language))
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            Converter = Converter,
            ConverterParameter = Key,
        };
}

/// <summary>Liefert für einen Schlüssel (ConverterParameter) den Text der aktuellen Sprache.</summary>
internal sealed class TrConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Loc.Instance[parameter as string ?? string.Empty];

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
