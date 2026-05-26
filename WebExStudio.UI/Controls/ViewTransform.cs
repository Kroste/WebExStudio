using System;
using Avalonia;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Reine (testbare) Zoom-/Pan-Transformation des Canvas. Bildet zwischen
/// <b>Viewport-Koordinaten</b> (die untransformierte Fläche, in der Mausrad und Cursor
/// gemessen werden) und <b>Welt-Koordinaten</b> (Node-Positionen) ab.
///
/// Wichtig: <see cref="ToWorld"/> erwartet Viewport-Koordinaten. Zeiger-Positionen relativ
/// zum <i>transformierten</i> NodeCanvas sind wegen dessen RenderTransform bereits
/// Welt-Koordinaten und dürfen NICHT erneut durch <see cref="ToWorld"/> laufen – sonst
/// driften Verbindung/Gummiband, sobald gezoomt oder gescrollt wurde.
/// </summary>
public readonly record struct ViewTransform(double Scale, double PanX, double PanY)
{
    public static readonly ViewTransform Identity = new(1, 0, 0);

    /// <summary>Viewport- → Welt-Koordinate (Umkehrung des RenderTransforms).</summary>
    public Point ToWorld(Point viewport) =>
        new((viewport.X - PanX) / Scale, (viewport.Y - PanY) / Scale);

    /// <summary>Welt- → Viewport-Koordinate (entspricht dem RenderTransform, Ursprung oben-links).</summary>
    public Point ToScreen(Point world) =>
        new(world.X * Scale + PanX, world.Y * Scale + PanY);

    /// <summary>
    /// Zoomt um einen Viewport-Pivot (der Punkt unter dem Cursor bleibt stehen) und clampt
    /// den Maßstab auf [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    public ViewTransform ZoomAround(Point pivot, double factor, double min, double max)
    {
        var newScale = Math.Clamp(Scale * factor, min, max);
        var f = newScale / Scale;
        return new ViewTransform(newScale, pivot.X - f * (pivot.X - PanX), pivot.Y - f * (pivot.Y - PanY));
    }
}
