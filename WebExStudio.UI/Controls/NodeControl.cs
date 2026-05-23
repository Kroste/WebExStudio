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
    public event EventHandler<(NodeViewModel vm, string slot)>? OpenSubTabRequested;

    private readonly Border _border;
    private readonly Border _statusIndicator;
    private readonly TextBlock _titleLabel;
    private readonly Border _header;
    private readonly Ellipse? _inputPort;
    private readonly Ellipse? _outputPort;
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

        _header = new Border
        {
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
                            _header,
                            new TextBlock
                            {
                                [DockPanel.DockProperty] = Dock.Bottom,
                                Text = vm.ActionType,
                                FontSize = 10,
                                Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                                Margin = new Thickness(8, 2, 8, 4),
                            }
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

        // Output port circle (bottom-center)
        if (vm.Definition.OutputPorts > 0)
        {
            _outputPort = MakePortEllipse();
            Children.Add(_outputPort);
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
        _outputPort?.Measure(availableSize);
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

        var cx = finalSize.Width / 2 - PortRadius;
        if (_inputPort is not null)
            _inputPort.Arrange(new Rect(cx, -PortRadius, PortRadius * 2, PortRadius * 2));
        if (_outputPort is not null)
            _outputPort.Arrange(new Rect(cx, finalSize.Height - PortRadius, PortRadius * 2, PortRadius * 2));

        return finalSize;
    }

    // ── Port hit-test ─────────────────────────────────────────────────────────

    public bool IsOnOutputPort(Point localPos)
    {
        var cx = Width / 2;
        var cy = Height;
        return Distance(localPos, cx, cy) <= PortRadius + 3;
    }

    public bool IsOnInputPort(Point localPos)
    {
        var cx = Width / 2;
        const double cy = 0;
        return Distance(localPos, cx, cy) <= PortRadius + 3;
    }

    private static double Distance(Point p, double x, double y) =>
        Math.Sqrt(Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2));

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var onPort = IsOnOutputPort(pos) || IsOnInputPort(pos);
        Cursor = onPort ? new Cursor(StandardCursorType.Cross) : new Cursor(StandardCursorType.SizeAll);

        if (_outputPort is not null)
            _outputPort.Fill = IsOnOutputPort(pos)
                ? Brushes.White
                : new SolidColorBrush(Color.Parse("#90A4AE"));
        if (_inputPort is not null)
            _inputPort.Fill = IsOnInputPort(pos)
                ? Brushes.White
                : new SolidColorBrush(Color.Parse("#90A4AE"));
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.SizeAll);
        if (_outputPort is not null) _outputPort.Fill = new SolidColorBrush(Color.Parse("#90A4AE"));
        if (_inputPort is not null) _inputPort.Fill = new SolidColorBrush(Color.Parse("#90A4AE"));
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.IsSelected)
            or nameof(NodeViewModel.IsActive)
            or nameof(NodeViewModel.Status)
            or nameof(NodeViewModel.StatusColor)
            or nameof(NodeViewModel.Color)
            or nameof(NodeViewModel.Title))
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

        if (ViewModel.IsActive)
        {
            _border.BorderBrush = new SolidColorBrush(Color.Parse("#FFD740"));
            _border.BorderThickness = new Thickness(3);
            _border.BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, Color = Color.Parse("#FFD740") });
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

        foreach (var slot in vm.Definition.SubFlowSlots)
        {
            var s = slot;
            var label = s switch
            {
                "then" => "Then-Tab öffnen",
                "else" => "Else-Tab öffnen",
                "body" => "Body-Tab öffnen",
                _ => $"{s}-Tab öffnen",
            };
            var item = new MenuItem { Header = $"📂 {label}" };
            item.Click += (_, _) => OpenSubTabRequested?.Invoke(this, (vm, s));
            menu.Items.Add(item);
        }

        if (vm.Definition.SubFlowSlots.Length > 0)
            menu.Items.Add(new Separator());

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
