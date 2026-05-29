using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// CPU-side coverage of the rounded-rect geometry emission. The shader itself is validated
/// separately through a headless WGPU device (naga); here we assert the vertex buffer the pipeline
/// consumes. No GPU device is needed — <see cref="Renderer2D"/>'s draw calls only populate vertex
/// lists; <c>Initialize</c>/<c>EndFrame</c> (which need a device) are not exercised.
/// </summary>
[TestClass]
public class RoundedRectGeometryTests
{
    private static readonly Vector4 Fill = new(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Vector4 Border = new(1f, 1f, 1f, 1f);

    [TestMethod]
    public void FilledRoundedRectEmitsSixVerticesWithZeroBorder()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(10f, 20f, 100f, 60f, 8f, Fill);

        var verts = renderer.RoundedVertices;
        Assert.AreEqual(6, verts.Count); // two triangles

        // Params = (halfWidth, halfHeight, radius, borderWidth).
        Assert.AreEqual(50f, verts[0].Params.X, 0.001f);
        Assert.AreEqual(30f, verts[0].Params.Y, 0.001f);
        Assert.AreEqual(8f, verts[0].Params.Z, 0.001f);
        Assert.AreEqual(0f, verts[0].Params.W, 0.001f); // filled => no border
        Assert.AreEqual(Fill, verts[0].Color);
    }

    [TestMethod]
    public void RadiusIsClampedToHalfTheShorterSide()
    {
        var renderer = new Renderer2D();
        // Height 40 => max radius 20, even though 999 was requested.
        renderer.DrawRoundedRect(0f, 0f, 200f, 40f, 999f, 2f, Fill, Border);

        Assert.AreEqual(20f, renderer.RoundedVertices[0].Params.Z, 0.001f);
        Assert.AreEqual(2f, renderer.RoundedVertices[0].Params.W, 0.001f);
        Assert.AreEqual(Border, renderer.RoundedVertices[0].BorderColor);
    }

    [TestMethod]
    public void LocalPositionsSpanTheBoxPlusAaPad()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(0f, 0f, 100f, 80f, 4f, Fill);

        // LocalPos corners are ±(halfSize + aaPad); aaPad is small (~1.5px), so each axis exceeds
        // the half-size by a little but stays well under half-size + a couple px.
        var maxX = 0f;
        var maxY = 0f;
        foreach (var v in renderer.RoundedVertices)
        {
            maxX = System.MathF.Max(maxX, System.MathF.Abs(v.LocalPos.X));
            maxY = System.MathF.Max(maxY, System.MathF.Abs(v.LocalPos.Y));
        }
        Assert.IsTrue(maxX > 50f && maxX < 53f, $"maxX={maxX}");
        Assert.IsTrue(maxY > 40f && maxY < 43f, $"maxY={maxY}");
    }

    [TestMethod]
    public void ZeroSizedRectEmitsNothing()
    {
        var renderer = new Renderer2D();
        renderer.DrawRoundedRectFilled(0f, 0f, 0f, 50f, 4f, Fill);
        Assert.AreEqual(0, renderer.RoundedVertices.Count);
    }
}
