using Avalonia;
using ReactiveUI;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

public sealed class NodeViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isSelected;
    private bool _isActive;
    private bool _isNext;
    private ExecutionStatusUi _status = ExecutionStatusUi.None;

    public FlowNode Model { get; }
    public string Id => Model.Id;
    public string ActionType => Model.Type;

    /// <summary>User-defined display name shown on the node (under the title).</summary>
    public string Label
    {
        get => Model.Label;
        set
        {
            if (Model.Label == value) return;
            Model.Label = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HasLabel));
        }
    }

    public bool HasLabel => !string.IsNullOrWhiteSpace(Model.Label);

    public NodeDefinition Definition { get; }

    public double X
    {
        get => _x;
        set
        {
            this.RaiseAndSetIfChanged(ref _x, value);
            Model.X = value;
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            this.RaiseAndSetIfChanged(ref _y, value);
            Model.Y = value;
        }
    }

    public double Width { get; } = 200;
    public double Height { get; } = 60;

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

    /// <summary>Der im pausierten Zustand als Nächstes auszuführende Node (eigene Markierung).</summary>
    public bool IsNext
    {
        get => _isNext;
        set => this.RaiseAndSetIfChanged(ref _isNext, value);
    }

    public ExecutionStatusUi Status
    {
        get => _status;
        set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(StatusColor));
        }
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

    public int OutputPorts => Math.Max(Definition.OutputPorts, 0);

    // Port positions (world coordinates) — used by ConnectionRenderer and wire drag
    public Point InputPortPosition => new(X + Width / 2, Y);

    /// <summary>Output port center, spaced evenly along the bottom edge.</summary>
    public Point OutputPortPosition(int port)
    {
        var n = Math.Max(OutputPorts, 1);
        var x = X + Width * (port + 1) / (n + 1.0);
        return new Point(x, Y + Height);
    }

    /// <summary>Visual-only annotation node (note, früher label/caption) — no ports, no execution.</summary>
    public bool IsAnnotation => ActionType is "label" or "caption" or "note";

    /// <summary>The on-canvas title. A call node shows the referenced subnode's name.</summary>
    public string Title =>
        ActionType == "call" && Model.Config.TryGetValue("target", out var t) && !string.IsNullOrEmpty(t)
            ? t
            : DisplayName;

    /// <summary>Raise after changing the call target so the on-canvas title updates.</summary>
    public void RaiseTitleChanged() => this.RaisePropertyChanged(nameof(Title));

    public NodeViewModel(FlowNode node)
    {
        Model = node;
        Definition = NodeCatalog.GetOrUnknown(node.Type);
        _x = node.X;
        _y = node.Y;
    }
}

public enum ExecutionStatusUi { None, Running, Success, Error, Skipped }
