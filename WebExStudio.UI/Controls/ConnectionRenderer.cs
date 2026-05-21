using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Transparent overlay that draws bezier connections between nodes.
/// Uses the same coordinate system as the NodeCanvas (transform is applied via code-behind).
/// </summary>
public sealed class ConnectionRenderer : Control
{
    private List<ConnectionViewModel> _connections = [];
    private List<NodeViewModel> _nodes = [];

    public void Update(IEnumerable<ConnectionViewModel> connections, IEnumerable<NodeViewModel> nodes)
    {
        _connections = connections.ToList();
        _nodes = nodes.ToList();
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        foreach (var conn in _connections)
            DrawConnection(ctx, conn);
    }

    private static void DrawConnection(DrawingContext ctx, ConnectionViewModel conn)
    {
        var src = conn.Source;
        var tgt = conn.Target;

        var p1 = new Point(src.X + src.Width / 2, src.Y + src.Height);
        var p2 = new Point(tgt.X + tgt.Width / 2, tgt.Y);

        var dy = Math.Abs(p2.Y - p1.Y) * 0.6 + 30;
        var c1 = new Point(p1.X, p1.Y + dy);
        var c2 = new Point(p2.X, p2.Y - dy);

        var geo = new PathGeometry();
        var figure = new PathFigure { StartPoint = p1, IsClosed = false };
        figure.Segments!.Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = p2 });
        geo.Figures.Add(figure);

        var strokeColor = Color.Parse(conn.StrokeColor);
        var pen = new Pen(new SolidColorBrush(strokeColor), 2.0)
        {
            DashStyle = conn.Kind == ConnectionKind.Sequential
                ? null
                : new DashStyle([6, 3], 0),
        };

        ctx.DrawGeometry(null, pen, geo);
        DrawArrowHead(ctx, p2, strokeColor);
    }

    private static void DrawArrowHead(DrawingContext ctx, Point tip, Color color)
    {
        const double size = 8;
        var left = new Point(tip.X - size / 2, tip.Y - size);
        var right = new Point(tip.X + size / 2, tip.Y - size);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = left, IsClosed = true };
        fig.Segments!.Add(new LineSegment { Point = tip });
        fig.Segments.Add(new LineSegment { Point = right });
        geo.Figures.Add(fig);

        ctx.DrawGeometry(new SolidColorBrush(color), null, geo);
    }
}
