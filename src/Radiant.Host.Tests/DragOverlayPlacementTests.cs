namespace Radiant.Host.Tests;

[TestClass]
public sealed class DragOverlayPlacementTests
{
    [TestMethod]
    public void WindowTopLeftCentresOnCursor()
    {
        var (x, y) = DragOverlayPlacement.WindowTopLeft(100f, 200f, 180, 30);
        Assert.AreEqual(10, x);  // 100 - 180/2
        Assert.AreEqual(185, y); // 200 - 30/2
    }

    [TestMethod]
    public void WindowTopLeftRoundsCursor()
    {
        var (x, y) = DragOverlayPlacement.WindowTopLeft(100.4f, 200.6f, 180, 30);
        Assert.AreEqual(10, x);
        Assert.AreEqual(186, y);
    }
}
