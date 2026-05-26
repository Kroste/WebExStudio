using Avalonia;
using WebExStudio.UI.Controls;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Zoom-/Pan-Mathematik des Canvas. Maus-Interaktion (Verbinden, Gummiband) ist selbst schwer
/// testbar (siehe CLAUDE.md) – getestet wird daher die zugrunde liegende Koordinaten-Logik.
/// Regression: Verbindung/Gummiband drifteten, weil bereits transformierte (Welt-)Positionen
/// ein zweites Mal durch <see cref="ViewTransform.ToWorld"/> liefen.
/// </summary>
public class ViewTransformTests
{
    private const double Eps = 1e-9;

    private static void AssertClose(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }

    [Fact]
    public void Identity_IsNoOp()
    {
        var v = ViewTransform.Identity;
        AssertClose(new Point(42, 7), v.ToWorld(new Point(42, 7)));
        AssertClose(new Point(42, 7), v.ToScreen(new Point(42, 7)));
    }

    [Fact]
    public void KnownMapping_ScaleAndPan()
    {
        var v = new ViewTransform(Scale: 2, PanX: 100, PanY: 50);
        // Welt (10,10) → Viewport (10*2+100, 10*2+50) = (120,70)
        AssertClose(new Point(120, 70), v.ToScreen(new Point(10, 10)));
        // und zurück
        AssertClose(new Point(10, 10), v.ToWorld(new Point(120, 70)));
    }

    [Theory]
    [InlineData(0.2, -300, 80)]
    [InlineData(1.0, 0, 0)]
    [InlineData(2.5, 640, -120)]
    public void RoundTrip_ToWorldInvertsToScreen(double scale, double panX, double panY)
    {
        var v = new ViewTransform(scale, panX, panY);
        var world = new Point(123.5, -47.25);
        AssertClose(world, v.ToWorld(v.ToScreen(world)));

        var viewport = new Point(15, 900);
        AssertClose(viewport, v.ToScreen(v.ToWorld(viewport)));
    }

    [Fact]
    public void ToWorld_OnAlreadyWorldPoint_DriftsWhenZoomedOrPanned()
    {
        // Dokumentiert die Ursache des Bugs: eine bereits in Welt-Koordinaten vorliegende
        // Position erneut durch ToWorld zu schicken, ist nur bei Identität harmlos.
        var identity = ViewTransform.Identity;
        AssertClose(new Point(200, 200), identity.ToWorld(new Point(200, 200)));

        var zoomedAndPanned = new ViewTransform(Scale: 2, PanX: 100, PanY: 100);
        var drifted = zoomedAndPanned.ToWorld(new Point(200, 200)); // (50,50) ≠ (200,200)
        Assert.True(System.Math.Abs(drifted.X - 200) > Eps);
        Assert.True(System.Math.Abs(drifted.Y - 200) > Eps);
    }

    [Fact]
    public void ZoomAround_KeepsPivotFixed()
    {
        var v = new ViewTransform(Scale: 1, PanX: 30, PanY: -10);
        var pivot = new Point(400, 250);
        var worldUnderPivot = v.ToWorld(pivot);

        var zoomed = v.ZoomAround(pivot, factor: 1.1, min: 0.2, max: 3.0);

        // Der Welt-Punkt unter dem Cursor bleibt nach dem Zoom an derselben Viewport-Stelle.
        AssertClose(pivot, zoomed.ToScreen(worldUnderPivot));
        Assert.Equal(1.1, zoomed.Scale, 6);
    }

    [Theory]
    [InlineData(100.0, 3.0)]   // weit über Max → auf Max geclamped
    [InlineData(0.0001, 0.2)]  // weit unter Min → auf Min geclamped
    public void ZoomAround_ClampsScale(double factor, double expectedScale)
    {
        var v = new ViewTransform(Scale: 1, PanX: 0, PanY: 0);
        var zoomed = v.ZoomAround(new Point(0, 0), factor, min: 0.2, max: 3.0);
        Assert.Equal(expectedScale, zoomed.Scale, 6);
    }
}
