using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Kroste-Standard-Titelleiste für Fenster mit <see cref="WindowDecorations.BorderOnly"/>:
/// Drag zum Verschieben, Doppelklick zum Maximieren, eigene Min/Max/Close-Buttons und ein
/// Slot (<see cref="Actions"/>) für fensterspezifische Schaltflächen.
///
/// Die Properties sind <see cref="StyledProperty{TValue}"/> und werden in
/// <see cref="OnPropertyChanged"/> von Hand an die Kindelemente durchgereicht: bei aktivierten
/// Compiled Bindings bräuchte ein <c>{Binding}</c> im Template ein <c>x:DataType</c>, das ein
/// wiederverwendbares Control nicht haben kann.
/// </summary>
public partial class TitleBar : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<TitleBar, string?>(nameof(Icon), defaultValue: "🌐");

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(Actions));

    /// <summary>Fenstertitel (Gold, links neben dem Icon).</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Piktogramm links im Titel (Emoji).</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Fensterspezifische Schaltflächen, links von Minimieren/Maximieren/Schließen.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public TitleBar()
    {
        InitializeComponent();

        IconText.Text = Icon;
        MinButton.Click += (_, _) => { if (Host is { } w) w.WindowState = WindowState.Minimized; };
        MaxButton.Click += (_, _) => ToggleMaximize();
        CloseButton.Click += (_, _) => Host?.Close();
        Bar.PointerPressed += OnBarPointerPressed;
        Bar.DoubleTapped += OnBarDoubleTapped;
    }

    /// <summary>
    /// ACHTUNG (Avalonia 12): <c>VisualRoot</c> ist NICHT mehr das Window — die Visual-Wurzel ist
    /// der interne TopLevelHost, das Window nur noch dessen Kind. <c>VisualRoot as Window</c>
    /// liefert null und macht alle Handler hier zu stillen No-Ops.
    /// </summary>
    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty) TitleText.Text = Title;
        else if (change.Property == IconProperty) IconText.Text = Icon;
        else if (change.Property == ActionsProperty) ActionsHost.Content = Actions;
    }

    private void OnBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // PFLICHT-Guard, siehe LandedOnInteractiveChild.
        if (LandedOnInteractiveChild(e.Source)) return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Host?.BeginMoveDrag(e);
    }

    private void OnBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LandedOnInteractiveChild(e.Source)) return;
        ToggleMaximize();
    }

    /// <summary>
    /// Läuft vom Ereignis-Ursprung den Visual-Baum hoch bis zur Titelleisten-Border und meldet
    /// true, wenn unterwegs ein interaktives Control liegt.
    ///
    /// WARUM: <c>PointerPressed</c> bubbelt. Ein Button fängt den Press selbst ab und captured den
    /// Pointer — eine ComboBox tut das NICHT. Ohne diesen Guard startet <c>BeginMoveDrag</c> einen
    /// Fenster-Drag, der Pointer wandert ans OS, und das Control sieht nie ein
    /// <c>PointerReleased</c>: das Dropdown lässt sich gar nicht mehr öffnen, nur der ToolTip
    /// erscheint. Die ElementRole-Rollen helfen dagegen NICHT — die regeln den OS-Hit-Test-Pfad,
    /// dieser Handler ist der managed Fallback und läuft davon unabhängig. Also nie als "dank
    /// ElementRole überflüssig" wegrefactoren.
    /// </summary>
    private bool LandedOnInteractiveChild(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
        {
            // Die Titelleiste selbst (und alles darüber) ist Drag-Fläche.
            if (ReferenceEquals(v, Bar)) return false;

            // Button deckt ToggleButton/CheckBox/RadioButton/RepeatButton mit ab.
            if (v is Button or ComboBox or TextBox or Slider or ListBox or MenuItem) return true;

            // Auffangnetz: alles Fokussierbare will den Klick selbst verarbeiten.
            if (v is InputElement { Focusable: true }) return true;
        }

        // Ursprung liegt außerhalb der Titelleiste (z. B. in einem Popup-Root).
        return true;
    }

    private void ToggleMaximize()
    {
        if (Host is { } w)
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }
}
