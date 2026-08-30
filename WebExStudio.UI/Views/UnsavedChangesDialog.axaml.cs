using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.Core.Localization;
using WebExStudio.UI.Text;

namespace WebExStudio.UI.Views;

/// <summary>Antwort auf „der Flow hat ungespeicherte Änderungen".</summary>
public enum UnsavedChangesAnswer
{
    /// <summary>Schließen abbrechen, Fenster bleibt offen.</summary>
    Cancel,
    /// <summary>Erst speichern, dann schließen.</summary>
    Save,
    /// <summary>Änderungen wegwerfen und schließen.</summary>
    Discard,
}

/// <summary>
/// Nachfrage vor dem Verwerfen ungespeicherter Flow-Änderungen.
///
/// WARUM: Der Editor führt <c>IsDirty</c> und zeigt es im Titel als <c>*</c> an — beim Schließen
/// wurde die Arbeit trotzdem kommentarlos weggeworfen. Escape und der Schließen-Knopf der
/// Titelleiste bedeuten hier bewusst „Abbrechen": wer aus Versehen schließt, verliert nichts.
/// </summary>
public partial class UnsavedChangesDialog : ChromeWindow
{
    /// <summary>Ergebnis des Dialogs; ohne Auswahl (Escape, Titelleisten-X) bleibt es Cancel.</summary>
    public UnsavedChangesAnswer Answer { get; private set; } = UnsavedChangesAnswer.Cancel;

    public UnsavedChangesDialog() : this(null) { }

    /// <param name="flowName">Angezeigter Flow-Name; null für einen noch namenlosen Flow.</param>
    public UnsavedChangesDialog(string? flowName)
    {
        InitializeComponent();
        Title = Loc.T("Unsaved_Title");
        MessageText.Text = WrapSafeText.Sanitize(
            string.Format(Loc.T("Unsaved_Message"), flowName ?? Loc.T("VM_Untitled")));
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Finish(UnsavedChangesAnswer.Save);
    private void OnDiscard(object? sender, RoutedEventArgs e) => Finish(UnsavedChangesAnswer.Discard);
    private void OnCancel(object? sender, RoutedEventArgs e) => Finish(UnsavedChangesAnswer.Cancel);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Finish(UnsavedChangesAnswer.Cancel);
        base.OnKeyDown(e);
    }

    private void Finish(UnsavedChangesAnswer answer)
    {
        Answer = answer;
        Close(answer);
    }
}
