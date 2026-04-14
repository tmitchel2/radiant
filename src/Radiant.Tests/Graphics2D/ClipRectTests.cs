using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class ClipRectTests
{
    [TestMethod]
    public void IntersectOfOverlappingRects_ReturnsOverlap()
    {
        var a = new Renderer2D.ClipRect(0, 0, 100, 100);
        var b = new Renderer2D.ClipRect(50, 50, 100, 100);

        var c = a.Intersect(b);

        Assert.AreEqual(50, c.X);
        Assert.AreEqual(50, c.Y);
        Assert.AreEqual(50, c.Width);
        Assert.AreEqual(50, c.Height);
    }

    [TestMethod]
    public void IntersectOfDisjointRects_ReturnsEmpty()
    {
        var a = new Renderer2D.ClipRect(0, 0, 10, 10);
        var b = new Renderer2D.ClipRect(100, 100, 10, 10);

        var c = a.Intersect(b);

        Assert.AreEqual(0, c.Width);
        Assert.AreEqual(0, c.Height);
    }

    [TestMethod]
    public void IntersectOfContainedRect_ReturnsInnerRect()
    {
        var outer = new Renderer2D.ClipRect(0, 0, 100, 100);
        var inner = new Renderer2D.ClipRect(20, 30, 10, 15);

        var c = outer.Intersect(inner);

        Assert.AreEqual(20, c.X);
        Assert.AreEqual(30, c.Y);
        Assert.AreEqual(10, c.Width);
        Assert.AreEqual(15, c.Height);
    }
}
