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

    public void AddEntry(TraceEntry entry) =>
        Entries.Add(new TraceEntryViewModel(entry));

    public void Clear() => Entries.Clear();
}
