using WebExStudio.UI.Controls;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Tests für die Raster-Mathematik des <see cref="GridOverlay"/>:
/// Offset-Normalisierung (auch für negative Pan-Werte) und der
/// Level-of-Detail-Schritt beim tiefen Rauszoomen.
/// </summary>
public class GridOverlayTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(7, 20, 7)]
    [InlineData(20, 20, 0)]     // exaktes Vielfaches → 0
    [InlineData(47, 20, 7)]
    [InlineData(-7, 20, 13)]    // negativ (nach links gepannt) → immer in [0, size)
    [InlineData(-20, 20, 0)]
    [InlineData(-47, 20, 13)]
    public void Wrap_NormalisiertInHalboffenesIntervall(double value, double size, double expected)
    {
        Assert.Equal(expected, GridOverlay.Wrap(value, size), precision: 9);
    }

    [Theory]
    [InlineData(20, 20)]   // Standardzoom: unverändert
    [InlineData(8, 8)]     // exakt an der Schwelle: unverändert
    [InlineData(7.9, 15.8)] // knapp darunter: einmal verdoppelt
    [InlineData(4, 8)]     // MinScale 0.2 → 4 px → 8 px
    [InlineData(3, 12)]    // 3 → 6 → 12 (zweimal verdoppelt)
    public void ComputeStep_VerdoppeltBisMindestabstand(double gridSize, double expected)
    {
        Assert.Equal(expected, GridOverlay.ComputeStep(gridSize), precision: 9);
    }

    [Fact]
    public void ComputeStep_BleibtAmWeltRasterAusgerichtet()
    {
        // Der LOD-Schritt ist immer ein Zweierpotenz-Vielfaches des Rasters,
        // damit sichtbare Punkte auf echten Rasterpositionen liegen.
        var grid = 4.5;
        var step = GridOverlay.ComputeStep(grid);
        var factor = step / grid;
        Assert.Equal(Math.Round(Math.Log2(factor)), Math.Log2(factor), precision: 9);
    }
}
