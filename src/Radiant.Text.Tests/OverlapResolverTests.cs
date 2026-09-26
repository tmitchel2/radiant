using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Text.Msdf;

namespace Radiant.Text.Tests;

/// <summary>Crossing contours rewritten as the outline of what the non-zero rule fills.</summary>
[TestClass]
public class OverlapResolverTests
{
    private static Shape Resolve(params float[][] polygons)
    {
        var builder = new GlyphOutline.Builder();
        foreach (var polygon in polygons)
        {
            builder.MoveTo(polygon[0], polygon[1]);
            for (var i = 2; i < polygon.Length; i += 2)
            {
                builder.LineTo(polygon[i], polygon[i + 1]);
            }
            builder.Close();
        }
        var shape = Shape.FromOutline(builder.Build());
        OverlapResolver.Resolve(shape);
        return shape;
    }

    // Clockwise with y up, as TrueType winds filled contours.
    private static float[] Square(float x0, float y0, float x1, float y1) => [x0, y0, x0, y1, x1, y1, x1, y0];

    [TestMethod]
    public void CrossingBarsBecomeOneOutline()
    {
        var shape = Resolve(Square(0, 10, 30, 20), Square(10, 0, 20, 30));

        Assert.AreEqual(1, shape.Contours.Count);
        Assert.AreEqual(12, shape.Contours[0].Edges.Count, "a plus has twelve sides");
        Assert.AreEqual(1, shape.Contours[0].Winding());
        Assert.IsTrue(shape.Validate());
    }

    [TestMethod]
    public void AContourInsideAnotherThatDoesntCrossItIsLeftToTheCombiner()
    {
        var shape = Resolve(Square(0, 0, 30, 30), Square(10, 10, 20, 20));

        // Nothing crosses, so nothing changes: msdfgen's overlapping-contour combiner already
        // ignores a filled contour inside another.
        Assert.AreEqual(2, shape.Contours.Count);
        var builder = new GlyphOutline.Builder();
        foreach (var square in new[] { Square(0, 0, 30, 30), Square(10, 10, 20, 20) })
        {
            builder.MoveTo(square[0], square[1]);
            for (var i = 2; i < square.Length; i += 2)
            {
                builder.LineTo(square[i], square[i + 1]);
            }
            builder.Close();
        }
        var field = MsdfGenerator.Generate(builder.Build(), 1f, 4f);
        // Texels along the inner square's left edge (x = 10) are ten pixels inside the outline.
        for (var y = 11; y < 19; y++)
        {
            Assert.AreEqual(2f, field.SignedDistance(10 - field.Left, -y - field.Top), 0.02f, $"y = {y}");
        }
    }

    [TestMethod]
    public void ASelfCrossingContourIsSplitIntoLoopsThatBothFill()
    {
        // A bow tie: the two triangles are wound opposite ways, and non-zero fills both.
        var shape = Resolve([0, 0, 10, 10, 10, 0, 0, 10]);

        Assert.AreEqual(2, shape.Contours.Count);
        Assert.IsTrue(shape.Contours.All(c => c.Winding() == 1 && c.Edges.Count == 3), "each triangle is filled on its right");
        Assert.IsTrue(shape.Validate());
    }

    [TestMethod]
    public void AHoleIsKeptAndAFilledContourOverItIsJoinedToTheRest()
    {
        // A ring (a square with a square hole) and a bar across the hole: the bar's part inside
        // the hole is filled, the rest of the hole stays open.
        var hole = new float[] { 10, 10, 20, 10, 20, 20, 10, 20 };
        var shape = Resolve(Square(0, 0, 30, 30), hole, Square(-5, 13, 35, 17));

        Assert.AreEqual(3, shape.Contours.Count, "the outline, and the hole split in two by the bar");
        Assert.AreEqual(1, shape.Contours.Count(c => c.Winding() == 1));
        Assert.AreEqual(2, shape.Contours.Count(c => c.Winding() == -1));
        Assert.IsTrue(shape.Validate());
    }

    [TestMethod]
    public void IdenticalContoursBecomeOne()
    {
        var shape = Resolve(Square(0, 0, 10, 10), Square(0, 0, 10, 10), Square(20, 0, 30, 10));

        Assert.AreEqual(2, shape.Contours.Count);
    }

    [TestMethod]
    public void ContoursThatDontCrossAreLeftExactlyAsTheyAre()
    {
        var builder = new GlyphOutline.Builder();
        builder.MoveTo(0, 0);
        builder.LineTo(0, 30);
        builder.QuadTo(15, 40, 30, 30);
        builder.LineTo(30, 0);
        builder.Close();
        builder.MoveTo(10, 10);
        builder.LineTo(20, 10);
        builder.LineTo(20, 20);
        builder.LineTo(10, 20);
        builder.Close();
        var shape = Shape.FromOutline(builder.Build());
        var edges = shape.Contours.SelectMany(c => c.Edges).ToArray();

        OverlapResolver.Resolve(shape);

        CollectionAssert.AreEqual(edges, shape.Contours.SelectMany(c => c.Edges).ToArray());
    }
}
