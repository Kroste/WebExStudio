using Avalonia.Data;
using Avalonia.Markup.Xaml;
using WebExStudio.Core.Localization;

namespace WebExStudio.UI.Localization;

/// <summary>
/// XAML-Markup-Extension für lokalisierte Texte: <c>{l:Tr Schlüssel}</c>.
/// Liefert eine Bindung an <see cref="Loc.Instance"/>, die sich bei Sprachwechsel
/// automatisch aktualisiert (Laufzeit-Umschaltung).
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
}
