namespace Radiant.Host.Tests;

[TestClass]
public sealed class TabStripLayoutTests
{
    [TestMethod]
    public void TabWidthSharesViewWhenBelowCap()
    {
        // 428 view − 28 reserved for the + button = 400 shared by 4 cells = 100 each.
        var layout = new TabStripLayout(428, 4);
        Assert.AreEqual(100f, layout.TabWidth, 1e-4f);
    }

    [TestMethod]
    public void TabWidthCappedAtMax()
    {
        var layout = new TabStripLayout(2000, 4); // 500 each → capped
        Assert.AreEqual(TabStripLayout.MaxTabWidth, layout.TabWidth, 1e-4f);
    }

    [TestMethod]
    public void EmptyStripHasZeroWidth()
    {
        var layout = new TabStripLayout(400, 0);
        Assert.AreEqual(0f, layout.TabWidth, 1e-4f);
        Assert.AreEqual(-1, layout.HitTest(10));
        Assert.AreEqual(0, layout.InsertionIndexAt(10));
    }

    [TestMethod]
    public void HitTestMapsXToCell()
    {
        var layout = new TabStripLayout(428, 4); // 400 shared → width 100
        Assert.AreEqual(0, layout.HitTest(0));
        Assert.AreEqual(0, layout.HitTest(99));
        Assert.AreEqual(1, layout.HitTest(100));
        Assert.AreEqual(3, layout.HitTest(350));
    }

    [TestMethod]
    public void HitTestRejectsOutsideStrip()
    {
        var layout = new TabStripLayout(428, 4); // width 100, cells span 0..400
        Assert.AreEqual(-1, layout.HitTest(-1));
        Assert.AreEqual(-1, layout.HitTest(400));
    }

    [TestMethod]
    public void InsertionIndexSnapsToNearestGap()
    {
        var layout = new TabStripLayout(428, 4); // width 100
        Assert.AreEqual(0, layout.InsertionIndexAt(0));
        Assert.AreEqual(0, layout.InsertionIndexAt(49));
        Assert.AreEqual(1, layout.InsertionIndexAt(50));
        Assert.AreEqual(1, layout.InsertionIndexAt(149));
        Assert.AreEqual(2, layout.InsertionIndexAt(150));
    }

    [TestMethod]
    public void InsertionIndexClampsToTabCount()
    {
        var layout = new TabStripLayout(428, 4);
        Assert.AreEqual(4, layout.InsertionIndexAt(10000));
    }

    [TestMethod]
    public void RectAtPositionsCell()
    {
        var layout = new TabStripLayout(428, 4); // 400 shared → width 100
        var rect = layout.RectAt(2);
        Assert.AreEqual(200f, rect.X, 1e-4f);
        Assert.AreEqual(100f, rect.Width, 1e-4f);
    }

    [TestMethod]
    public void CloseRectInsetAtRightOfWideCell()
    {
        var layout = new TabStripLayout(400, 4); // width 100 (wide enough)
        var cell = layout.RectAt(1);
        var close = layout.CloseRectAt(1);
        Assert.IsNotNull(close);
        Assert.AreEqual(TabStripLayout.CloseButtonSize, close!.Value.Width, 1e-4f);
        // Right edge of the close button sits one margin in from the cell's right edge.
        var expectedRight = cell.X + cell.Width - TabStripLayout.CloseButtonMargin;
        Assert.AreEqual(expectedRight, close.Value.X + close.Value.Width, 1e-4f);
    }

    [TestMethod]
    public void CloseRectNullForNarrowCell()
    {
        // 12 tabs in 400px → ~33px cells, below MinWidthForCloseButton.
        var layout = new TabStripLayout(400, 12);
        Assert.IsTrue(layout.TabWidth < TabStripLayout.MinWidthForCloseButton);
        Assert.IsNull(layout.CloseRectAt(0));
    }

    [TestMethod]
    public void PlusRectSitsRightAfterCappedTabs()
    {
        // Few wide tabs (capped at MaxTabWidth) → + button just past the last cell, not at the strip edge.
        var layout = new TabStripLayout(2000, 2); // capped at 180 each
        Assert.AreEqual(2 * TabStripLayout.MaxTabWidth, layout.PlusRect.X, 1e-4f);
        Assert.AreEqual(TabStripLayout.PlusButtonWidth, layout.PlusRect.Width, 1e-4f);
    }

    [TestMethod]
    public void PlusRectLandsInReservedColumnWhenTabsFillStrip()
    {
        // Many tabs share the reserved remainder → + button occupies the reserved right column.
        var layout = new TabStripLayout(428, 4); // 400 shared, 28 reserved
        Assert.AreEqual(428f - TabStripLayout.PlusButtonWidth, layout.PlusRect.X, 1e-4f);
    }
}
