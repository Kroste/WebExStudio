using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Transparent overlay drawing wires between node ports and the active drag-preview line.
/// Uses the same world transform as NodeCanvas.
/// </summary>
public sealed class ConnectionRenderer : Control
{
    private List<WireViewModel> _wires = [];
    private Dictionary<string, NodeViewModel> _nodeMap = new();
    private Point? _dragFrom;
    private Point? _dragTo;
    private Rect? _selectionRect;
    private WireViewModel? _selectedWire;

    public WireViewModel? SelectedWire
    {
        get => _selectedWire;
        set { _selectedWire = value; InvalidateVisual(); }
    }

    /// <summary>Returns the wire nearest to <paramref name="world"/> within the threshold, or null.</summary>
    public WireViewModel? HitTest(Point world, double threshold = 10)
    {
        foreach (var wire in _wires)
        {
            if (!_nodeMap.TryGetValue(wire.SourceNodeId, out var src)) continue;
            if (!_nodeMap.TryGetValue(wire.TargetNodeId, out var tgt)) continue;
            if (DistanceToWire(world, src.OutputPortPosition(wire.OutputPort), tgt.InputPortPosition) <= threshold)
                return wire;
        }
        return null;
    }

    private static double DistanceToWire(Point p, Point from, Point to)
    {
        var dy = Math.Abs(to.Y - from.Y) * 0.6 + 30;
        var c1 = new Point(from.X, from.Y + dy);
        var c2 = new Point(to.X, to.Y - dy);

        const int steps = 20;
        var best = double.MaxValue;
        var prev = from;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var pt = CubicBezier(from, c1, c2, to, t);
            best = Math.Min(best, DistanceToSegment(p, prev, pt));
            prev = pt;
        }
        return best;
    }

    private static Point CubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var w0 = u * u * u;
        var w1 = 3 * u * u * t;
        var w2 = 3 * u * t * t;
        var w3 = t * t * t;
        return new Point(
            w0 * p0.X + w1 * p1.X + w2 * p2.X + w3 * p3.X,
            w0 * p0.Y + w1 * p1.Y + w2 * p2.Y + w3 * p3.Y);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-6) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return Distance(p, proj);
    }

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    public void Update(IEnumerable<WireViewModel> wires, IEnumerable<NodeViewModel> nodes)
    {
        _wires = wires.ToList();
        _nodeMap = nodes.ToDictionary(n => n.Id);
        InvalidateVisual();
    }

    public void SetDragPreview(Point from, Point to)
    {
        _dragFrom = from;
        _dragTo = to;
        InvalidateVisual();
    }

    public void ClearDragPreview()
    {
        _dragFrom = null;
        _dragTo = null;
        InvalidateVisual();
    }

    public void SetSelectionRect(Rect world)
    {
        _selectionRect = world;
        InvalidateVisual();
    }

    public void ClearSelectionRect()
    {
        _selectionRect = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        // Draw all wires
        foreach (var wire in _wires)
        {
            if (!_nodeMap.TryGetValue(wire.SourceNodeId, out var src)) continue;
            if (!_nodeMap.TryGetValue(wire.TargetNodeId, out var tgt)) continue;
            DrawWire(ctx, src.OutputPortPosition(wire.OutputPort), tgt.InputPortPosition, false, wire == _selectedWire);
        }

        // Draw drag preview
        if (_dragFrom.HasValue && _dragTo.HasValue)
            DrawWire(ctx, _dragFrom.Value, _dragTo.Value, true, false);

        // Draw rubber-band selection rectangle
        if (_selectionRect is { } sel)
        {
            var fill = new SolidColorBrush(Color.Parse("#4FC3F7"), 0.15);
            var pen = new Pen(new SolidColorBrush(Color.Parse("#4FC3F7")), 1)
            {
                DashStyle = new DashStyle([4, 3], 0),
            };
            ctx.DrawRectangle(fill, pen, sel);
        }
    }

    private static void DrawWire(DrawingContext ctx, Point from, Point to, bool isPreview, bool isSelected)
    {
        var dy = Math.Abs(to.Y - from.Y) * 0.6 + 30;
        var c1 = new Point(from.X, from.Y + dy);
        var c2 = new Point(to.X, to.Y - dy);

        var geo = new PathGeometry();
        var figure = new PathFigure { StartPoint = from, IsClosed = false };
        (figure.Segments ??= []).Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = to });
        (geo.Figures ??= []).Add(figure);

        var color = isPreview ? Color.Parse("#FFFFFF88")
                  : isSelected ? Color.Parse("#FF5252")
                  : Color.Parse("#90A4AE");
        var pen = new Pen(new SolidColorBrush(color), isSelected ? 3.0 : 2.0)
        {
            DashStyle = isPreview ? new DashStyle([6, 3], 0) : null,
        };

        ctx.DrawGeometry(null, pen, geo);

        if (!isPreview)
            DrawArrowHead(ctx, to, color);
    }

    private static void DrawArrowHead(DrawingContext ctx, Point tip, Color color)
    {
        const double size = 8;
        var left = new Point(tip.X - size / 2, tip.Y - size);
        var right = new Point(tip.X + size / 2, tip.Y - size);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = left, IsClosed = true };
        var segs = fig.Segments ??= [];
        segs.Add(new LineSegment { Point = tip });
        segs.Add(new LineSegment { Point = right });
        (geo.Figures ??= []).Add(fig);

        ctx.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}
