using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NLog;

namespace WebExStudio.UI.Views;

public partial class HelpWindow : Window
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public HelpWindow()
    {
        InitializeComponent();
        HelpContent.Content = MarkdownRenderer.Build(LoadReadme());
    }

    /// <summary>Liest die eingebettete README (Quelle der Hilfe → bleibt automatisch synchron).</summary>
    private static string LoadReadme()
    {
        try
        {
            var asm = typeof(HelpWindow).Assembly;
            using var stream = asm.GetManifestResourceStream("WebExStudio.UI.README.md");
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            Log.Warn("Hilfe: eingebettete README nicht gefunden.");
        }
        catch (System.Exception ex)
        {
            Log.Warn("Hilfe: README konnte nicht geladen werden: {0}", ex.Message);
        }
        return "# Hilfe\n\nDie eingebettete README konnte nicht geladen werden. "
             + "Die vollständige Dokumentation liegt als `README.md` im Projektverzeichnis.";
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleClose(object? _, RoutedEventArgs e) => Close();
}
