using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Zoomable/pannable canvas that renders nodes and wire connections.
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

    // ── Node drag state (supports moving a whole selection together) ──────────
    private NodeViewModel? _draggingNode;
    private Point _dragStart;
    private readonly List<NodeViewModel> _dragNodes = [];
    private readonly List<Point> _dragNodeStarts = [];

    // ── Wire drag state ──────────────────────────────────────────────────────
    private NodeViewModel? _wireDragSource;
    private int _wireDragOutputPort;
    private Point _wireDragStartWorld;

    // ── Rubber-band selection state ───────────────────────────────────────────
    private bool _rubberPending;
    private bool _rubberActive;
    private Point _rubberStartWorld;
    private const double RubberThreshold = 4;

    /// <summary>Fired when a rubber-band drag finishes, with the selected (world) rectangle.</summary>
    public event EventHandler<Rect>? SelectionCompleted;

    public GridOverlay? GridOverlay { get; set; }
    public ConnectionRenderer? ConnectionRenderer { get; set; }

    /// <summary>Fired when the user completes a wire drag from source → target node.</summary>
    public event EventHandler<WireDropEventArgs>? WireDropped;

    /// <summary>The shared zoom+pan transform — assign to ConnectionRenderer.RenderTransform so both stay in sync.</summary>
    public TransformGroup WorldTransform => (TransformGroup)RenderTransform!;

    public NodeCanvas()
    {
        var group = new TransformGroup();
        group.Children.Add(_zoom);
        group.Children.Add(_pan);
        RenderTransform = group;
        // Ursprung oben-links statt Mitte: passend zur CanvasToWorld-Mathematik und
        // unabhängig von der Fenstergröße (sonst driften Nodes/Wires nach einem Resize).
        RenderTransformOrigin = RelativePoint.TopLeft;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        // Hinweis: Das Mausrad wird NICHT hier, sondern auf der untransformierten Canvas-Fläche
        // behandelt (FlowEditorView → ApplyWheel). Sonst wandert die Trefferfläche mit dem Pan
        // und Scrollen stoppt, sobald der Cursor außerhalb des verschobenen Canvas liegt.
    }

    public void ResetView()
    {
        _scale = 1.0;
        _panOffsetX = 0;
        _panOffsetY = 0;
        UpdateTransform();
    }

    /// <summary>Verschiebt die Ansicht so, dass der Welt-Punkt in der Mitte des Canvas liegt.</summary>
    public void CenterOn(Point world)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        _panOffsetX = Bounds.Width / 2 - _scale * world.X;
        _panOffsetY = Bounds.Height / 2 - _scale * world.Y;
        UpdateTransform();
    }

    /// <summary>Aktuelle Zoom-/Pan-Transformation als reiner (testbarer) Wert.</summary>
    private ViewTransform View => new(_scale, _panOffsetX, _panOffsetY);

    public Point CanvasToWorld(Point viewport) => View.ToWorld(viewport);

    public Point WorldToCanvas(Point world) => View.ToScreen(world);

    /// <summary>
    /// Zeiger-Position im <b>untransformierten</b> Viewport (Eltern-Container). Notwendig, weil
    /// <c>GetPosition(this)</c> wegen des RenderTransforms bereits Welt-Koordinaten liefert –
    /// die dürfen nicht noch einmal durch <see cref="CanvasToWorld"/>. Relativ zum Eltern-
    /// Element gemessen erhalten wir hingegen echte Viewport-Koordinaten.
    /// </summary>
    private Point ViewportPos(PointerEventArgs e) =>
        e.GetCurrentPoint(this.GetVisualParent() ?? this).Position;

    internal void BeginNodeDrag(NodeViewModel node, IEnumerable<NodeViewModel> alsoMove, Point canvasPos, PointerPressedEventArgs e)
    {
        _draggingNode = node;
        _dragStart = CanvasToWorld(canvasPos);

        _dragNodes.Clear();
        _dragNodeStarts.Clear();
        var set = new List<NodeViewModel>(alsoMove);
        if (!set.Contains(node)) set.Add(node);
        foreach (var n in set)
        {
            _dragNodes.Add(n);
            _dragNodeStarts.Add(new Point(n.X, n.Y));
        }
        e.Pointer.Capture(this);
    }

    internal void BeginWireDrag(NodeViewModel source, int outputPort, PointerPressedEventArgs e)
    {
        _wireDragSource = source;
        _wireDragOutputPort = outputPort;
        _wireDragStartWorld = source.OutputPortPosition(outputPort);
        e.Pointer.Capture(this);
    }

    public bool IsWireDragging => _wireDragSource is not null;

    public void CancelWireDrag()
    {
        _wireDragSource = null;
        ConnectionRenderer?.ClearDragPreview();
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
        var viewport = ViewportPos(e);
        if (pt.Properties.IsMiddleButtonPressed ||
            (pt.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning = true;
            _panStart = viewport;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (pt.Properties.IsLeftButtonPressed)
        {
            // Plain left press on empty canvas: arm a rubber-band. We don't capture or handle
            // yet so the view still gets the click for wire selection; a drag promotes it.
            _rubberPending = true;
            _rubberStartWorld = CanvasToWorld(viewport);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = ViewportPos(e);

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
            var dx = world.X - _dragStart.X;
            var dy = world.Y - _dragStart.Y;
            for (var i = 0; i < _dragNodes.Count; i++)
            {
                _dragNodes[i].X = _dragNodeStarts[i].X + dx;
                _dragNodes[i].Y = _dragNodeStarts[i].Y + dy;
            }
            e.Handled = true;
            return;
        }

        if (_wireDragSource is not null && ConnectionRenderer is not null)
        {
            var worldPos = CanvasToWorld(pos);
            ConnectionRenderer.SetDragPreview(_wireDragStartWorld, worldPos);
            e.Handled = true;
            return;
        }

        if (_rubberPending || _rubberActive)
        {
            var world = CanvasToWorld(pos);
            if (!_rubberActive)
            {
                if (Math.Abs(world.X - _rubberStartWorld.X) < RubberThreshold &&
                    Math.Abs(world.Y - _rubberStartWorld.Y) < RubberThreshold) return;
                _rubberActive = true;
                _rubberPending = false;
                e.Pointer.Capture(this);
            }
            ConnectionRenderer?.SetSelectionRect(Normalize(_rubberStartWorld, world));
            e.Handled = true;
        }
    }

    private static Rect Normalize(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_wireDragSource is not null)
        {
            e.Pointer.Capture(null);
            var worldPos = CanvasToWorld(ViewportPos(e));
            WireDropped?.Invoke(this, new WireDropEventArgs(_wireDragSource, _wireDragOutputPort, worldPos));
            _wireDragSource = null;
            ConnectionRenderer?.ClearDragPreview();
            e.Handled = true;
        }
        else if (_rubberActive)
        {
            e.Pointer.Capture(null);
            var rect = Normalize(_rubberStartWorld, CanvasToWorld(ViewportPos(e)));
            ConnectionRenderer?.ClearSelectionRect();
            SelectionCompleted?.Invoke(this, rect);
            e.Handled = true;
        }
        else if (_isPanning || _draggingNode is not null)
        {
            e.Pointer.Capture(null);
        }

        _isPanning = false;
        _draggingNode = null;
        _dragNodes.Clear();
        _dragNodeStarts.Clear();
        _rubberPending = false;
        _rubberActive = false;
    }

    /// <summary>Zoom/Pan per Mausrad. <paramref name="pos"/> ist die Cursor-Position in der
    /// untransformierten Viewport-Fläche (FlowEditorView reicht sie durch), damit das Scrollen
    /// unabhängig vom aktuellen Pan/Zoom über die gesamte Ansicht funktioniert.</summary>
    public void ApplyWheel(Vector delta, Point pos, KeyModifiers mods)
    {
        if (mods.HasFlag(KeyModifiers.Control))
        {
            var zoomed = View.ZoomAround(pos, Math.Pow(1.1, delta.Y), MinScale, MaxScale);
            _scale = zoomed.Scale;
            _panOffsetX = zoomed.PanX;
            _panOffsetY = zoomed.PanY;
        }
        else if (mods.HasFlag(KeyModifiers.Shift))
        {
            _panOffsetX += delta.Y * 100;
        }
        else
        {
            _panOffsetY += delta.Y * 100;
        }
        UpdateTransform();
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

public sealed class WireDropEventArgs(NodeViewModel source, int outputPort, Point worldPos) : EventArgs
{
    public NodeViewModel Source { get; } = source;
    public int OutputPort { get; } = outputPort;
    public Point WorldPos { get; } = worldPos;
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
