using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class ScrollOffsetTests
{
    private static (float min, float max) FilledYRange(Renderer2D r, int from, int count)
    {
        var ys = r.FilledVertices.Skip(from).Take(count).Select(v => v.Position.Y).ToList();
        return (ys.Min(), ys.Max());
    }

    [TestMethod]
    public void GeometryEmittedInsidePushIsTranslated()
    {
        var r = new Renderer2D();
        r.DrawFilledRect(new Vector2(0, 50), new Vector2(10, 10), Vector4.One);
        var afterFirst = r.FilledVertices.Count;

        r.PushScrollOffset(new Vector2(0, -30));
        r.DrawFilledRect(new Vector2(0, 50), new Vector2(10, 10), Vector4.One);
        r.PopScrollOffset();

        // First rect untouched.
        var (min0, max0) = FilledYRange(r, 0, afterFirst);
        Assert.AreEqual(50f, min0, 0.01f);
        Assert.AreEqual(60f, max0, 0.01f);

        // Second rect shifted up by 30.
        var (min1, max1) = FilledYRange(r, afterFirst, r.FilledVertices.Count - afterFirst);
        Assert.AreEqual(20f, min1, 0.01f);
        Assert.AreEqual(30f, max1, 0.01f);
    }

    [TestMethod]
    public void NestedPushesComposeCumulatively()
    {
        var r = new Renderer2D();
        r.PushScrollOffset(new Vector2(0, -10));
        var outerStart = r.FilledVertices.Count;
        r.DrawFilledRect(new Vector2(0, 100), new Vector2(10, 10), Vector4.One); // outer-only

        var innerStart = r.FilledVertices.Count;
        r.PushScrollOffset(new Vector2(0, -5));
        r.DrawFilledRect(new Vector2(0, 100), new Vector2(10, 10), Vector4.One); // inner
        r.PopScrollOffset();
        r.PopScrollOffset();

        var (outerMin, _) = (r.FilledVertices.Skip(outerStart).Take(innerStart - outerStart).Min(v => v.Position.Y), 0f);
        var innerMin = r.FilledVertices.Skip(innerStart).Min(v => v.Position.Y);

        Assert.AreEqual(90f, outerMin, 0.01f, "outer-only geometry shifted by -10");
        Assert.AreEqual(85f, innerMin, 0.01f, "inner geometry shifted by -(10+5)");
    }

    [TestMethod]
    public void ZeroOffsetIsNoOp()
    {
        var r = new Renderer2D();
        r.PushScrollOffset(Vector2.Zero);
        r.DrawFilledRect(new Vector2(0, 70), new Vector2(10, 10), Vector4.One);
        r.PopScrollOffset();

        var (min, max) = FilledYRange(r, 0, r.FilledVertices.Count);
        Assert.AreEqual(70f, min, 0.01f);
        Assert.AreEqual(80f, max, 0.01f);
    }
}
