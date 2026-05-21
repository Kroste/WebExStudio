using ReactiveUI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

public sealed class TargetViewModel : ViewModelBase
{
    private string _name;
    private string _host;
    private string _actionsFile;
    private bool _enabled;

    public TargetConfig Model { get; }

    public string Name
    {
        get => _name;
        set { this.RaiseAndSetIfChanged(ref _name, value); Model.Name = value; }
    }

    public string Host
    {
        get => _host;
        set { this.RaiseAndSetIfChanged(ref _host, value); Model.Host = value; }
    }

    public string ActionsFile
    {
        get => _actionsFile;
        set { this.RaiseAndSetIfChanged(ref _actionsFile, value); Model.ActionsFile = value; }
    }

    public bool Enabled
    {
        get => _enabled;
        set { this.RaiseAndSetIfChanged(ref _enabled, value); Model.Enabled = value; }
    }

    public TargetViewModel(TargetConfig model)
    {
        Model = model;
        _name = model.Name;
        _host = model.Host;
        _actionsFile = model.ActionsFile;
        _enabled = model.Enabled;
    }
}
