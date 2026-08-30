using System.Collections.ObjectModel;
using ReactiveUI;
using WebExStudio.Engine;

namespace WebExStudio.UI.ViewModels;

public sealed class TracePanelViewModel : ViewModelBase
{
    private bool _autoScroll = true;
    private string _filter = string.Empty;

    public ObservableCollection<TraceEntryViewModel> Entries { get; } = [];

    public bool AutoScroll
    {
        get => _autoScroll;
        set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
    }

    public string Filter
    {
        get => _filter;
        set => this.RaiseAndSetIfChanged(ref _filter, value);
    }

    /// <summary>
    /// Obergrenze für das Protokoll. Ohne Deckel wächst die Liste mit jeder Node-Ausführung weiter —
    /// eine Schleife über ein paar tausend Elemente lässt Speicher und Renderaufwand ungebremst
    /// steigen. Die ältesten Einträge fallen hinten raus; interessant ist am Ende ohnehin, was
    /// zuletzt passiert ist.
    /// </summary>
    public const int MaxEntries = 5000;

    public void AddEntry(TraceEntry entry)
    {
        Entries.Add(new TraceEntryViewModel(entry));
        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(0);
    }

    public void Clear() => Entries.Clear();
}
