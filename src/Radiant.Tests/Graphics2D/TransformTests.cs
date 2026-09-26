using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

/// <summary>How <see cref="Renderer2D.PushTransform"/> moves recorded geometry.</summary>
[TestClass]
public class TransformTests
{
    private static readonly Vector4 White = Vector4.One;

    [TestMethod]
    public void GeometryInsideATransformIsTransformed()
    {
        var r = new Renderer2D();

        r.PushTransform(Matrix3x2.CreateScale(2f));
        r.DrawRectangleFilled(1, 1, 1, 1, White);
        r.PopTransform();

        var xs = r.FilledVertices.Select(v => v.Position.X).ToList();
        Assert.AreEqual(2f, xs.Min(), 1e-5f);
        Assert.AreEqual(4f, xs.Max(), 1e-5f);
    }

    [TestMethod]
    public void GeometryOutsideATransformIsUntouched()
    {
        var r = new Renderer2D();
        r.DrawRectangleFilled(1, 1, 1, 1, White);

        r.PushTransform(Matrix3x2.CreateTranslation(100, 0));
        r.PopTransform();
        r.DrawRectangleFilled(1, 1, 1, 1, White);

        Assert.IsTrue(r.FilledVertices.All(v => v.Position.X <= 2f));
    }

    [TestMethod]
    public void NestedTransformsApplyTheInnerOneFirst()
    {
        var r = new Renderer2D();

        // Scale (inner) then translate (outer): the point at x=1 goes to 2, then to 12.
        r.PushTransform(Matrix3x2.CreateTranslation(10, 0));
        r.PushTransform(Matrix3x2.CreateScale(2f));
        r.DrawRectangleFilled(1, 0, 1, 1, White);
        r.PopTransform();
        r.PopTransform();

        Assert.AreEqual(12f, r.FilledVertices.Min(v => v.Position.X), 1e-5f);
    }

    [TestMethod]
    public void AShapesQuadMovesButItsLocalFrameDoesNot()
    {
        var r = new Renderer2D();
        r.DrawRoundedRectFilled(0, 0, 20, 10, 2f, White);
        var before = r.SdfShapeVertices.ToList();

        var r2 = new Renderer2D();
        r2.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 2));
        r2.DrawRoundedRectFilled(0, 0, 20, 10, 2f, White);
        r2.PopTransform();

        for (var i = 0; i < before.Count; i++)
        {
            Assert.AreEqual(before[i].LocalPos, r2.SdfShapeVertices[i].LocalPos, "the SDF is evaluated in the shape's own frame");
            Assert.AreNotEqual(before[i].Position, r2.SdfShapeVertices[i].Position);
        }
    }

    [TestMethod]
    public void ScrollOffsetsAreTranslationsOnTheSameStack()
    {
        var r = new Renderer2D();

        r.PushTransform(Matrix3x2.CreateScale(2f));
        r.PushScrollOffset(new Vector2(0, -5));
        r.DrawRectangleFilled(0, 10, 1, 1, White);
        r.PopScrollOffset();
        r.PopTransform();

        // Scrolled to y=5 inside, then scaled to 10.
        Assert.AreEqual(10f, r.FilledVertices.Min(v => v.Position.Y), 1e-5f);
    }

    [TestMethod]
    public void AnUnmatchedPopIsIgnored()
    {
        var r = new Renderer2D();

        r.PopTransform();
        r.DrawRectangleFilled(1, 1, 1, 1, White);

        Assert.AreEqual(1f, r.FilledVertices.Min(v => v.Position.X));
    }
}
