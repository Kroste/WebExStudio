using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NLog;

namespace WebExStudio.UI.Views;

/// <summary>
/// Basisklasse für ALLE Fenster der App (Kroste-Standard): Custom-Chrome nach
/// Avalonia-12-Konvention plus einheitliches Fenster-Icon.
///
/// Die vier Chrome-Zeilen sind Pflicht, zwei Fallen stecken darin:
/// <list type="bullet">
/// <item><b>BorderOnly, niemals None.</b> <c>None</c> nimmt dem Fenster die nativen
///   Resize-Griffe und den Schatten — das Fenster lässt sich dann nur noch über
///   selbstgebaute Griffe in der Größe ändern.</item>
/// <item><b>Ohne ExtendClientAreaToDecorationsHint + TitleBarHeightHint = -1 ist die
///   eigene Titelleiste tot</b>: die OS-Caption-Hit-Test-Zone liegt über dem oberen
///   Fensterbereich und schluckt Klicks und Drag-Events.</item>
/// </list>
/// </summary>
public class ChromeWindow : Window
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    protected ChromeWindow()
    {
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
        // Alle Fenster sind größenveränderbar — auch Dialoge und Einstellungen.
        // Wer eine Mindestgröße braucht, setzt MinWidth/MinHeight statt CanResize="False".
        CanResize = true;

        TryLoadIcon();
    }

    /// <summary>
    /// Einheitliches App-Icon aus den Avalonia-Ressourcen. Fehlt es, läuft das Fenster
    /// ohne Icon weiter — ein fehlendes Bild darf kein Fenster verhindern.
    /// </summary>
    private void TryLoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://WebExStudio/Assets/webexstudio.png"));
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Fenster-Icon konnte nicht geladen werden — Fenster startet ohne Icon");
        }
    }
}
