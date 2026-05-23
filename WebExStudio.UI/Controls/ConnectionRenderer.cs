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

    public override void Render(DrawingContext ctx)
    {
        // Draw all wires
        foreach (var wire in _wires)
        {
            if (!_nodeMap.TryGetValue(wire.SourceNodeId, out var src)) continue;
            if (!_nodeMap.TryGetValue(wire.TargetNodeId, out var tgt)) continue;
            DrawWire(ctx, src.OutputPortPosition, tgt.InputPortPosition, false);
        }

        // Draw drag preview
        if (_dragFrom.HasValue && _dragTo.HasValue)
            DrawWire(ctx, _dragFrom.Value, _dragTo.Value, true);
    }

    private static void DrawWire(DrawingContext ctx, Point from, Point to, bool isPreview)
    {
        var dy = Math.Abs(to.Y - from.Y) * 0.6 + 30;
        var c1 = new Point(from.X, from.Y + dy);
        var c2 = new Point(to.X, to.Y - dy);

        var geo = new PathGeometry();
        var figure = new PathFigure { StartPoint = from, IsClosed = false };
        (figure.Segments ??= []).Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = to });
        (geo.Figures ??= []).Add(figure);

        var color = isPreview ? Color.Parse("#FFFFFF88") : Color.Parse("#90A4AE");
        var pen = new Pen(new SolidColorBrush(color), 2.0)
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
