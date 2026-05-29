using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// CPU-side coverage of the batched SDF-shape geometry emission (rounded rect / disc / ring). The
/// shader itself is validated separately through a headless WGPU device (naga); here we assert the
/// vertex buffer the pipeline consumes. No GPU device is needed — <see cref="Renderer2D"/>'s draw
/// calls only populate vertex lists.
/// </summary>
[TestClass]
public class SdfShapeGeometryTests
{
    private static readonly Vector4 Fill = new(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Vector4 Border = new(1f, 1f, 1f, 1f);

    private const float RoundedRect = 0f;
    private const float Circle = 1f;

    [TestMethod]
    public void FilledRoundedRectEmitsSixVerticesAsRoundedRectKind()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(10f, 20f, 100f, 60f, 8f, Fill);

        var verts = renderer.SdfShapeVertices;
        Assert.AreEqual(6, verts.Count); // two triangles

        // Misc = (halfWidth, halfHeight, borderWidth, shapeKind).
        Assert.AreEqual(50f, verts[0].Misc.X, 0.001f);
        Assert.AreEqual(30f, verts[0].Misc.Y, 0.001f);
        Assert.AreEqual(0f, verts[0].Misc.Z, 0.001f); // filled => no border
        Assert.AreEqual(RoundedRect, verts[0].Misc.W, 0.001f);
        // Uniform radius broadcast to all four corners.
        Assert.AreEqual(new Vector4(8f, 8f, 8f, 8f), verts[0].Params);
        Assert.AreEqual(Fill, verts[0].Color);
    }

    [TestMethod]
    public void PerCornerRadiiArePreservedInCssOrder()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(0f, 0f, 200f, 200f,
            new CornerRadii(TopLeft: 4f, TopRight: 8f, BottomRight: 12f, BottomLeft: 16f), Fill);

        // Params order matches the shader: (TL, TR, BR, BL).
        Assert.AreEqual(new Vector4(4f, 8f, 12f, 16f), renderer.SdfShapeVertices[0].Params);
    }

    [TestMethod]
    public void EachCornerRadiusIsClampedToHalfTheShorterSide()
    {
        var renderer = new Renderer2D();
        // Height 40 => max radius 20, even though larger values were requested per corner.
        renderer.DrawRoundedRect(0f, 0f, 200f, 40f, CornerRadii.All(999f), 2f, Fill, Border);

        var p = renderer.SdfShapeVertices[0].Params;
        Assert.AreEqual(new Vector4(20f, 20f, 20f, 20f), p);
        Assert.AreEqual(2f, renderer.SdfShapeVertices[0].Misc.Z, 0.001f); // border width preserved
        Assert.AreEqual(Border, renderer.SdfShapeVertices[0].BorderColor);
    }

    [TestMethod]
    public void DiscEmitsCircleKindWithZeroInnerRadius()
    {
        var renderer = new Renderer2D();
        renderer.DrawDisc(new Vector2(50f, 50f), 25f, Fill);

        var v = renderer.SdfShapeVertices[0];
        Assert.AreEqual(Circle, v.Misc.W, 0.001f);
        Assert.AreEqual(25f, v.Misc.X, 0.001f); // halfSize = radius
        Assert.AreEqual(25f, v.Params.X, 0.001f); // outer radius
        Assert.AreEqual(0f, v.Params.Y, 0.001f);  // inner radius (disc => 0)
    }

    [TestMethod]
    public void RingCarriesOuterAndInnerRadius()
    {
        var renderer = new Renderer2D();
        renderer.DrawRing(new Vector2(0f, 0f), outerRadius: 30f, innerRadius: 22f, Fill);

        var v = renderer.SdfShapeVertices[0];
        Assert.AreEqual(Circle, v.Misc.W, 0.001f);
        Assert.AreEqual(30f, v.Params.X, 0.001f);
        Assert.AreEqual(22f, v.Params.Y, 0.001f);
    }

    [TestMethod]
    public void LocalPositionsSpanTheBoxPlusAaPad()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(0f, 0f, 100f, 80f, 4f, Fill);

        var maxX = 0f;
        var maxY = 0f;
        foreach (var v in renderer.SdfShapeVertices)
        {
            maxX = MathF.Max(maxX, MathF.Abs(v.LocalPos.X));
            maxY = MathF.Max(maxY, MathF.Abs(v.LocalPos.Y));
        }
        Assert.IsTrue(maxX > 50f && maxX < 53f, $"maxX={maxX}");
        Assert.IsTrue(maxY > 40f && maxY < 43f, $"maxY={maxY}");
    }

    [TestMethod]
    public void DegenerateShapesEmitNothing()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(0f, 0f, 0f, 50f, 4f, Fill);
        renderer.DrawDisc(new Vector2(0f, 0f), 0f, Fill);
        Assert.AreEqual(0, renderer.SdfShapeVertices.Count);
    }
}
