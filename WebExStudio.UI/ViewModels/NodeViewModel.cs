using System.ComponentModel;
using Avalonia;
using ReactiveUI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

public sealed class NodeViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isSelected;
    private bool _isActive;
    private bool _isNext;
    private string? _validationError;
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
    /// <summary>Validierungsfehler an diesem Node (null = ok) — wird als Marker angezeigt.</summary>
    public string? ValidationError
    {
        get => _validationError;
        set
        {
            if (_validationError == value) return;
            this.RaiseAndSetIfChanged(ref _validationError, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_validationError);

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

    public string DisplayName => NodeCatalog.LocalizedName(Definition);
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
        // Anzeigename/Titel bei Sprachwechsel aktualisieren (der gespeicherte Node-Typ bleibt unberührt).
        Loc.Instance.PropertyChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(DisplayName));
        this.RaisePropertyChanged(nameof(Title));
    }
}

public enum ExecutionStatusUi { None, Running, Success, Error, Skipped }
