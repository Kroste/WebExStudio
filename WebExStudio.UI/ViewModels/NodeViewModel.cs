using System.Collections.ObjectModel;
using ReactiveUI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

public sealed class NodeViewModel : ViewModelBase
{
    private readonly ActionNode _node;

    private double _x;
    private double _y;
    private bool _isSelected;
    private bool _isActive;
    private bool _isExpanded = true;
    private ExecutionStatusUi _status = ExecutionStatusUi.None;

    public string Id => _node.EnsureUi().Id;
    public string ActionType => _node.Type;
    public ActionNode Model => _node;

    public NodeDefinition Definition { get; }
    public bool HasSubActions => Definition.HasSubActions;

    // Sub-node collections populated on load from inline then/else/actions or file refs
    public ObservableCollection<NodeViewModel> ThenNodes { get; } = new();
    public ObservableCollection<NodeViewModel> ElseNodes { get; } = new();
    public ObservableCollection<NodeViewModel> BodyNodes { get; } = new();

    // True when ThenNodes was loaded from then_actions_file (not inlined on save)
    public bool ThenFromFile { get; set; }

    public IEnumerable<NodeViewModel> AllSubNodes =>
        ThenNodes.Concat(ElseNodes).Concat(BodyNodes);

    public ObservableCollection<NodeViewModel> GetBranch(string key) => key switch
    {
        "then" => ThenNodes,
        "else" => ElseNodes,
        _ => BodyNodes,
    };

    public double X
    {
        get => _x;
        set
        {
            this.RaiseAndSetIfChanged(ref _x, value);
            _node.EnsureUi().X = value;
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            this.RaiseAndSetIfChanged(ref _y, value);
            _node.EnsureUi().Y = value;
        }
    }

    public double Width => _node.Ui?.Width ?? 200;
    public double Height => _node.Ui?.Height ?? 60;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ExecutionStatusUi Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public string DisplayName => Definition.DisplayName;
    public string Icon => Definition.Icon;
    public string Color => Definition.Color;

    public string StatusColor => Status switch
    {
        ExecutionStatusUi.Running => "#FFC107",
        ExecutionStatusUi.Success => "#4CAF50",
        ExecutionStatusUi.Error => "#F44336",
        ExecutionStatusUi.Skipped => "#9E9E9E",
        _ => Definition.Color,
    };

    public NodeViewModel(ActionNode node)
    {
        _node = node;
        Definition = NodeCatalog.GetOrUnknown(node.Type);
        var ui = node.EnsureUi();
        _x = ui.X;
        _y = ui.Y;
    }
}

public enum ExecutionStatusUi { None, Running, Success, Error, Skipped }
