using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NLog;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Visual representation of a single node on the canvas.
/// Extends Panel so that port circles can be positioned partly outside the node border.
/// </summary>
public sealed class NodeControl : Panel
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private const double PortRadius = 6;

    public NodeViewModel ViewModel { get; }
    public event EventHandler<NodeViewModel>? DeleteRequested;

    private readonly Border _border;
    private readonly Border _statusIndicator;
    private readonly TextBlock _titleLabel;
    private readonly TextBlock _labelText;
    private readonly Border _header;
    private readonly Ellipse? _inputPort;
    private readonly List<Ellipse> _outputPorts = [];
    private readonly List<TextBlock> _outputLabels = [];
    private readonly TextBlock? _annotation;

    public NodeControl(NodeViewModel vm)
    {
        ViewModel = vm;
        Width = vm.Width;
        Height = vm.Height;
        Cursor = new Cursor(StandardCursorType.SizeAll);
        ClipToBounds = false;

        _statusIndicator = new Border
        {
            Width = 4,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(6, 0, 0, 6),
        };

        _titleLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // User-defined free-text name shown under the title.
        _labelText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#E0E0E0")),
            FontSize = 12,
            FontStyle = FontStyle.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(8, 0, 8, 0),
        };

        _header = new Border
        {
            [DockPanel.DockProperty] = Dock.Top,
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(8, 0, 4, 0),
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = vm.Icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center },
                    _titleLabel,
                }
            }
        };

        _border = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("4,*"),
                Children =
                {
                    _statusIndicator,
                    new DockPanel
                    {
                        [Grid.ColumnProperty] = 1,
                        Children =
                        {
                            _header, // Dock.Top set below
                            new TextBlock
                            {
                                [DockPanel.DockProperty] = Dock.Bottom,
                                Text = vm.ActionType,
                                FontSize = 10,
                                Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                                Margin = new Thickness(8, 2, 8, 4),
                            },
                            _labelText, // fills the middle (LastChildFill)
                        }
                    }
                }
            }
        };
        // Annotation nodes (label/caption): text only, no box, no ports.
        if (vm.IsAnnotation)
        {
            var isCaption = vm.ActionType == "caption";
            Width = 280;
            Height = isCaption ? 48 : 60;
            _annotation = new TextBlock
            {
                Text = vm.Model.Get("text", isCaption ? "Überschrift" : "Kommentar"),
                Foreground = isCaption ? Brushes.White : new SolidColorBrush(Color.Parse("#B0BEC5")),
                FontSize = isCaption ? 22 : 13,
                FontWeight = isCaption ? FontWeight.Bold : FontWeight.Normal,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Children.Add(_annotation);
            ContextMenu = BuildContextMenu(vm);
            vm.PropertyChanged += OnVmPropertyChanged;
            return;
        }

        Children.Add(_border);

        // Input port circle (top-center)
        if (vm.Definition.InputPorts > 0)
        {
            _inputPort = MakePortEllipse();
            Children.Add(_inputPort);
        }

        // Output port circles (bottom, spaced) with optional labels
        var labels = vm.Definition.OutputLabels;
        for (int i = 0; i < vm.Definition.OutputPorts; i++)
        {
            var port = MakePortEllipse();
            _outputPorts.Add(port);
            Children.Add(port);

            if (vm.Definition.OutputPorts > 1)
            {
                var lbl = new TextBlock
                {
                    Text = i < labels.Length ? labels[i] : i.ToString(),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                    IsHitTestVisible = false,
                };
                _outputLabels.Add(lbl);
                Children.Add(lbl);
            }
        }

        ContextMenu = BuildContextMenu(vm);
        vm.PropertyChanged += OnVmPropertyChanged;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        UpdateVisuals();
    }

    private static Ellipse MakePortEllipse() => new()
    {
        Width = PortRadius * 2,
        Height = PortRadius * 2,
        Fill = new SolidColorBrush(Color.Parse("#90A4AE")),
        Stroke = Brushes.DarkGray,
        StrokeThickness = 1.5,
        IsHitTestVisible = false,
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_annotation is not null)
        {
            _annotation.Measure(availableSize);
            return new Size(Width, Height);
        }
        _border.Measure(availableSize);
        _inputPort?.Measure(availableSize);
        foreach (var p in _outputPorts) p.Measure(availableSize);
        foreach (var l in _outputLabels) l.Measure(availableSize);
        return new Size(Width, Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_annotation is not null)
        {
            _annotation.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        _border.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

        if (_inputPort is not null)
            _inputPort.Arrange(new Rect(finalSize.Width / 2 - PortRadius, -PortRadius, PortRadius * 2, PortRadius * 2));

        for (int i = 0; i < _outputPorts.Count; i++)
        {
            var x = OutputPortX(i, finalSize.Width);
            _outputPorts[i].Arrange(new Rect(x - PortRadius, finalSize.Height - PortRadius, PortRadius * 2, PortRadius * 2));
            if (i < _outputLabels.Count)
                _outputLabels[i].Arrange(new Rect(x - 18, finalSize.Height - PortRadius - 12, 36, 12));
        }

        return finalSize;
    }

    /// <summary>Local X of output port i (matches NodeViewModel.OutputPortPosition spacing).</summary>
    private double OutputPortX(int port, double width)
    {
        var n = Math.Max(_outputPorts.Count, 1);
        return width * (port + 1) / (n + 1.0);
    }

    // ── Port hit-test ─────────────────────────────────────────────────────────

    /// <summary>Returns the output-port index under the point, or -1 if none.</summary>
    public int OutputPortAt(Point localPos)
    {
        for (int i = 0; i < _outputPorts.Count; i++)
            if (Distance(localPos, OutputPortX(i, Width), Height) <= PortRadius + 3)
                return i;
        return -1;
    }

    public bool IsOnOutputPort(Point localPos) => OutputPortAt(localPos) >= 0;

    public bool IsOnInputPort(Point localPos)
    {
        var cx = Width / 2;
        const double cy = 0;
        return Distance(localPos, cx, cy) <= PortRadius + 3;
    }

    private static double Distance(Point p, double x, double y) =>
        Math.Sqrt(Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2));

    // ── Events ────────────────────────────────────────────────────────────────

    private static readonly IBrush PortIdle = new SolidColorBrush(Color.Parse("#90A4AE"));

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var hitOut = OutputPortAt(pos);
        var onPort = hitOut >= 0 || IsOnInputPort(pos);
        Cursor = onPort ? new Cursor(StandardCursorType.Cross) : new Cursor(StandardCursorType.SizeAll);

        for (int i = 0; i < _outputPorts.Count; i++)
            _outputPorts[i].Fill = i == hitOut ? Brushes.White : PortIdle;
        if (_inputPort is not null)
            _inputPort.Fill = IsOnInputPort(pos) ? Brushes.White : PortIdle;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.SizeAll);
        foreach (var p in _outputPorts) p.Fill = PortIdle;
        if (_inputPort is not null) _inputPort.Fill = PortIdle;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.IsSelected)
            or nameof(NodeViewModel.IsActive)
            or nameof(NodeViewModel.IsNext)
            or nameof(NodeViewModel.Status)
            or nameof(NodeViewModel.StatusColor)
            or nameof(NodeViewModel.Color)
            or nameof(NodeViewModel.Title)
            or nameof(NodeViewModel.Label))
        {
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        var baseColor = Color.Parse(ViewModel.Color);
        var statusColor = Color.Parse(ViewModel.StatusColor);

        _border.Background = new SolidColorBrush(Color.FromArgb(220,
            (byte)(baseColor.R / 3),
            (byte)(baseColor.G / 3),
            (byte)(baseColor.B / 3)));

        _header.Background = new SolidColorBrush(Color.FromArgb(200,
            baseColor.R, baseColor.G, baseColor.B));

        _statusIndicator.Background = new SolidColorBrush(statusColor);
        _titleLabel.Text = ViewModel.Title;
        _labelText.Text = ViewModel.Label;
        _labelText.IsVisible = ViewModel.HasLabel;

        if (ViewModel.IsActive)
        {
            _border.BorderBrush = new SolidColorBrush(Color.Parse("#FFD740"));
            _border.BorderThickness = new Thickness(3);
            _border.BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, Color = Color.Parse("#FFD740") });
        }
        else if (ViewModel.IsNext)
        {
            // Nächster auszuführender Node im pausierten Zustand (Cyan).
            _border.BorderBrush = new SolidColorBrush(Color.Parse("#00E5FF"));
            _border.BorderThickness = new Thickness(3);
            _border.BoxShadow = new BoxShadows(new BoxShadow { Blur = 16, Color = Color.Parse("#00E5FF") });
        }
        else if (ViewModel.IsSelected)
        {
            _border.BorderBrush = new SolidColorBrush(Colors.White);
            _border.BorderThickness = new Thickness(2);
            _border.BoxShadow = BoxShadows.Parse("0 0 8 #FFFFFF44");
        }
        else
        {
            _border.BorderBrush = new SolidColorBrush(Color.Parse(ViewModel.Color));
            _border.BorderThickness = new Thickness(2);
            _border.BoxShadow = BoxShadows.Parse("0 2 6 #00000066");
        }
    }

    private ContextMenu BuildContextMenu(NodeViewModel vm)
    {
        var menu = new ContextMenu();

        var deleteItem = new MenuItem { Header = "🗑 Node löschen" };
        deleteItem.Click += (_, _) =>
        {
            Log.Info("Node löschen: {0} ({1})", vm.Id, vm.ActionType);
            DeleteRequested?.Invoke(this, vm);
        };
        menu.Items.Add(deleteItem);

        return menu;
    }
}
