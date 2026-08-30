using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// CPU-side coverage of the width-carrying line and the arbitrary quad. WebGPU has no lineWidth, so
/// <see cref="Renderer2D.DrawLine"/> is always one physical pixel and anything wider has to be
/// geometry; these assert the triangles that geometry produces. No GPU device is needed — the draw
/// calls only populate vertex lists.
/// </summary>
[TestClass]
public class ThickLineGeometryTests
{
    private static readonly Vector4 Fill = new(0.2f, 0.4f, 0.6f, 1f);

    [TestMethod]
    public void ThickLineEmitsSixVertices()
    {
        var renderer = new Renderer2D();
        renderer.DrawThickLine(new Vector2(0f, 0f), new Vector2(100f, 0f), 10f, Fill);

        Assert.AreEqual(6, renderer.FilledVertices.Count); // two triangles
        Assert.AreEqual(Fill, renderer.FilledVertices[0].Color);
    }

    [TestMethod]
    public void AHorizontalLineIsOffsetVerticallyByHalfItsWidth()
    {
        var renderer = new Renderer2D();
        renderer.DrawThickLine(new Vector2(0f, 50f), new Vector2(100f, 50f), 10f, Fill);

        foreach (var vertex in renderer.FilledVertices)
        {
            Assert.AreEqual(5f, Math.Abs(vertex.Position.Y - 50f), 0.001f);
            Assert.IsTrue(vertex.Position.X is >= -0.001f and <= 100.001f);
        }
    }

    [TestMethod]
    public void AVerticalLineIsOffsetHorizontally()
    {
        var renderer = new Renderer2D();
        renderer.DrawThickLine(new Vector2(20f, 0f), new Vector2(20f, 80f), 4f, Fill);

        foreach (var vertex in renderer.FilledVertices)
        {
            Assert.AreEqual(2f, Math.Abs(vertex.Position.X - 20f), 0.001f);
        }
    }

    /// <summary>
    /// The width is measured perpendicular to the segment, so a diagonal is offset by half the
    /// width along both axes scaled by the direction — not by half the width on each. This is the
    /// case a naive axis-aligned implementation gets wrong.
    /// </summary>
    [TestMethod]
    public void ADiagonalIsOffsetPerpendicularlyRatherThanPerAxis()
    {
        var renderer = new Renderer2D();
        var from = new Vector2(0f, 0f);
        var to = new Vector2(100f, 100f);

        renderer.DrawThickLine(from, to, 10f, Fill);

        var direction = Vector2.Normalize(to - from);

        foreach (var vertex in renderer.FilledVertices)
        {
            // Distance from the infinite line through `from` and `to`, by the 2D cross product.
            var offset = vertex.Position - from;
            var distance = Math.Abs((direction.X * offset.Y) - (direction.Y * offset.X));

            Assert.AreEqual(5f, distance, 0.001f);
        }
    }

    [TestMethod]
    public void AZeroLengthLineEmitsNothing()
    {
        var renderer = new Renderer2D();
        renderer.DrawThickLine(new Vector2(7f, 7f), new Vector2(7f, 7f), 10f, Fill);

        Assert.AreEqual(0, renderer.FilledVertices.Count);
    }

    [TestMethod]
    public void AZeroWidthLineEmitsNothing()
    {
        var renderer = new Renderer2D();
        renderer.DrawThickLine(new Vector2(0f, 0f), new Vector2(10f, 0f), 0f, Fill);

        Assert.AreEqual(0, renderer.FilledVertices.Count);
    }

    [TestMethod]
    public void QuadEmitsTwoTrianglesSharingTheDiagonal()
    {
        var renderer = new Renderer2D();
        var a = new Vector2(0f, 0f);
        var b = new Vector2(10f, 0f);
        var c = new Vector2(10f, 10f);
        var d = new Vector2(0f, 10f);

        renderer.DrawQuad(a, b, c, d, Fill);

        Assert.AreEqual(6, renderer.FilledVertices.Count);
        Assert.AreEqual(a, renderer.FilledVertices[0].Position);
        Assert.AreEqual(b, renderer.FilledVertices[1].Position);
        Assert.AreEqual(c, renderer.FilledVertices[2].Position);
        Assert.AreEqual(a, renderer.FilledVertices[3].Position);
        Assert.AreEqual(c, renderer.FilledVertices[4].Position);
        Assert.AreEqual(d, renderer.FilledVertices[5].Position);
    }

    [TestMethod]
    public void TriangleEmitsThreeVertices()
    {
        var renderer = new Renderer2D();
        renderer.DrawTriangle(new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(0f, 5f), Fill);

        Assert.AreEqual(3, renderer.FilledVertices.Count);
    }
}
