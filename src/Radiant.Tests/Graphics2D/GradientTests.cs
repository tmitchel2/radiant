using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class GradientTests
{
    private static readonly Vector4 A = Vector4.Zero;
    private static readonly Vector4 B = Vector4.One;

    [TestMethod]
    public void OneStopIsNotAGradient()
    {
        Assert.ThrowsException<ArgumentException>(() => Gradient.Linear(Vector2.Zero, Vector2.One, [new GradientStop(0, A)]));
    }

    [TestMethod]
    public void MoreStopsThanTheShaderCarriesAreRefused()
    {
        GradientStop[] five = [new(0, A), new(0.25f, B), new(0.5f, A), new(0.75f, B), new(1, A)];

        Assert.ThrowsException<ArgumentException>(() => Gradient.Linear(Vector2.Zero, Vector2.One, five));
    }

    [TestMethod]
    public void StopsMustBeInOrder()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            Gradient.Radial(Vector2.Zero, 1f, [new GradientStop(0.6f, A), new GradientStop(0.4f, B)]));
    }

    [TestMethod]
    public void TheDefaultSpaceIsSrgbAsInCss()
    {
        Assert.AreEqual(GradientInterpolation.Srgb, Gradient.Linear(Vector2.Zero, Vector2.One, A, B).Interpolation);
    }

    [TestMethod]
    public void AGradientIsStoredInTheShapesOwnFrame()
    {
        var r = new Renderer2D();

        r.DrawRoundedRectFilled(10, 10, 20, 20, CornerRadii.All(0f), Gradient.Linear(new Vector2(10, 20), new Vector2(30, 20), A, B));

        // The rect is centred on (20, 20), so the line runs from (-10, 0) to (10, 0) locally.
        Assert.AreEqual(new Vector4(-10, 0, 10, 0), r.SdfShapeVertices[0].GradientGeometry);
        Assert.AreEqual(new Vector4((int)GradientKind.Linear, 2, (int)GradientInterpolation.Srgb, 0), r.SdfShapeVertices[0].GradientInfo);
    }
}
