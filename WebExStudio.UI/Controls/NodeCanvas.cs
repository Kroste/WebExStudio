using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Zoomable/pannable canvas that renders nodes and bezier connections.
/// Nodes are placed as child controls; the grid is drawn by the GridOverlay below.
/// </summary>
public sealed class NodeCanvas : Canvas
{
    // ── Pan state ───────────────────────────────────────────────────────────
    private bool _isPanning;
    private Point _panStart;
    private double _panOffsetX;
    private double _panOffsetY;

    // ── Zoom ────────────────────────────────────────────────────────────────
    private double _scale = 1.0;
    private const double MinScale = 0.2;
    private const double MaxScale = 3.0;

    private readonly ScaleTransform _zoom = new();
    private readonly TranslateTransform _pan = new();

    // ── Drag state for nodes ─────────────────────────────────────────────
    private NodeViewModel? _draggingNode;
    private Point _dragStart;
    private Point _nodeStartPos;

    public GridOverlay? GridOverlay { get; set; }

    /// <summary>The shared zoom+pan transform — assign to ConnectionRenderer.RenderTransform so both stay in sync.</summary>
    public TransformGroup WorldTransform => (TransformGroup)RenderTransform!;

    public NodeCanvas()
    {
        var group = new TransformGroup();
        group.Children.Add(_zoom);
        group.Children.Add(_pan);
        RenderTransform = group;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    public void ResetView()
    {
        _scale = 1.0;
        _panOffsetX = 0;
        _panOffsetY = 0;
        UpdateTransform();
    }

    public Point CanvasToWorld(Point screen) =>
        new((screen.X - _panOffsetX) / _scale, (screen.Y - _panOffsetY) / _scale);

    internal void BeginNodeDrag(NodeViewModel node, Point canvasPos, PointerPressedEventArgs e)
    {
        _draggingNode = node;
        _dragStart = CanvasToWorld(canvasPos);
        _nodeStartPos = new Point(node.X, node.Y);
        e.Pointer.Capture(this);
    }

    public void FitToView(IEnumerable<Rect> nodeBounds)
    {
        var rects = nodeBounds.ToList();
        if (rects.Count == 0) { ResetView(); return; }

        var minX = rects.Min(r => r.Left);
        var minY = rects.Min(r => r.Top);
        var maxX = rects.Max(r => r.Right);
        var maxY = rects.Max(r => r.Bottom);
        var contentW = maxX - minX;
        var contentH = maxY - minY;
        var viewW = Bounds.Width;
        var viewH = Bounds.Height;
        if (viewW <= 0 || viewH <= 0) { ResetView(); return; }

        const double padding = 60;
        var scaleX = contentW > 0 ? (viewW - padding * 2) / contentW : 1;
        var scaleY = contentH > 0 ? (viewH - padding * 2) / contentH : 1;
        _scale = Math.Clamp(Math.Min(scaleX, scaleY), MinScale, MaxScale);
        _panOffsetX = (viewW - contentW * _scale) / 2 - minX * _scale;
        _panOffsetY = (viewH - contentH * _scale) / 2 - minY * _scale;
        UpdateTransform();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsMiddleButtonPressed ||
            (pt.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning = true;
            _panStart = pt.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;
        if (_isPanning)
        {
            _panOffsetX += pos.X - _panStart.X;
            _panOffsetY += pos.Y - _panStart.Y;
            _panStart = pos;
            UpdateTransform();
            e.Handled = true;
            return;
        }
        if (_draggingNode is not null)
        {
            var world = CanvasToWorld(pos);
            _draggingNode.X = _nodeStartPos.X + (world.X - _dragStart.X);
            _draggingNode.Y = _nodeStartPos.Y + (world.Y - _dragStart.Y);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning || _draggingNode is not null)
            e.Pointer.Capture(null);
        _isPanning = false;
        _draggingNode = null;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var mouse = e.GetPosition(this);
        var delta = Math.Pow(1.1, e.Delta.Y);
        var newScale = Math.Clamp(_scale * delta, MinScale, MaxScale);
        var factor = newScale / _scale;
        _panOffsetX = mouse.X - factor * (mouse.X - _panOffsetX);
        _panOffsetY = mouse.Y - factor * (mouse.Y - _panOffsetY);
        _scale = newScale;
        UpdateTransform();
        e.Handled = true;
    }

    private void UpdateTransform()
    {
        _zoom.ScaleX = _scale;
        _zoom.ScaleY = _scale;
        _pan.X = _panOffsetX;
        _pan.Y = _panOffsetY;
        GridOverlay?.UpdateOffset(_panOffsetX, _panOffsetY, _scale);
    }
}

/// <summary>Draws the background dot-grid behind the canvas (not transformed, just offset).</summary>
public sealed class GridOverlay : Control
{
    private double _ox, _oy, _scale = 1;

    public void UpdateOffset(double ox, double oy, double scale)
    {
        _ox = ox; _oy = oy; _scale = scale;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#12121F")), new Rect(Bounds.Size));

        var gridSize = 20.0 * _scale;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#252540")), 0.5);
        var dotBrush = new SolidColorBrush(Color.Parse("#303050"));

        var ox = _ox % gridSize;
        var oy = _oy % gridSize;

        for (var x = ox - gridSize; x < Bounds.Width + gridSize; x += gridSize)
        {
            ctx.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
            for (var y = oy - gridSize; y < Bounds.Height + gridSize; y += gridSize)
                ctx.DrawEllipse(dotBrush, null, new Point(x, y), 1.5, 1.5);
        }
    }
}
