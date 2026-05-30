using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.UI;

namespace Radiant.Tests.Layout;

[TestClass]
public class YogaLayoutTests
{
    private static Panel Root(Vector2 position, Vector2 size, LayoutStyle style)
    {
        var panel = new Panel(TestFonts.Default)
        {
            Position = position,
            Size = size,
            ParticipatesInLayout = true,
            Layout = style,
        };
        return panel;
    }

    [TestMethod]
    public void RowGrowSplitsWidthEvenlyAndOffsetsByRootOrigin()
    {
        var root = Root(new Vector2(10, 20), new Vector2(200, 100),
            new LayoutStyle { FlexDirection = FlexDirection.Row });
        var a = root.Add(new Label(TestFonts.Default) { Layout = new LayoutStyle { FlexGrow = 1f } });
        var b = root.Add(new Label(TestFonts.Default) { Layout = new LayoutStyle { FlexGrow = 1f } });

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        // Two grow:1 children share 200px => 100px each; absolute X offset by root origin (10).
        Assert.AreEqual(10f, a.Position.X, 0.01f);
        Assert.AreEqual(100f, a.Size.X, 0.01f);
        Assert.AreEqual(110f, b.Position.X, 0.01f);
        Assert.AreEqual(100f, b.Size.X, 0.01f);
        // alignItems defaults to stretch => full cross-axis height.
        Assert.AreEqual(100f, a.Size.Y, 0.01f);
        Assert.AreEqual(20f, a.Position.Y, 0.01f);
    }

    [TestMethod]
    public void ColumnFixedHeaderPlusGrowContent()
    {
        var root = Root(Vector2.Zero, new Vector2(200, 300),
            new LayoutStyle { FlexDirection = FlexDirection.Column });
        var header = root.Add(new Label(TestFonts.Default) { Layout = new LayoutStyle { Height = 40f } });
        var content = root.Add(new Label(TestFonts.Default) { Layout = new LayoutStyle { FlexGrow = 1f } });

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        Assert.AreEqual(0f, header.Position.Y, 0.01f);
        Assert.AreEqual(40f, header.Size.Y, 0.01f);
        Assert.AreEqual(40f, content.Position.Y, 0.01f);
        Assert.AreEqual(260f, content.Size.Y, 0.01f);
    }

    [TestMethod]
    public void PaddingInsetsChildren()
    {
        var root = Root(Vector2.Zero, new Vector2(200, 200),
            new LayoutStyle { Padding = Edges.All(10f) });
        var child = root.Add(new Label(TestFonts.Default) { Layout = new LayoutStyle { FlexGrow = 1f } });

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        Assert.AreEqual(10f, child.Position.X, 0.01f);
        Assert.AreEqual(10f, child.Position.Y, 0.01f);
        Assert.AreEqual(180f, child.Size.X, 0.01f);
        Assert.AreEqual(180f, child.Size.Y, 0.01f);
    }

    [TestMethod]
    public void PercentWidthIsRelativeToParent()
    {
        var root = Root(Vector2.Zero, new Vector2(400, 100),
            new LayoutStyle { FlexDirection = FlexDirection.Row });
        var child = root.Add(new Label(TestFonts.Default)
        {
            Layout = new LayoutStyle { Width = Dimension.Percent(25f) },
        });

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        Assert.AreEqual(100f, child.Size.X, 0.01f); // 25% of 400
    }

    [TestMethod]
    public void TextLeafIsSizedByItsMeasureCallback()
    {
        var root = Root(Vector2.Zero, new Vector2(400, 100),
            new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.FlexStart });
        var label = root.Add(new Label(TestFonts.Default, "Hello", Vector2.Zero));

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        var pixelHeight = label.TextScale * 7f;
        var expectedWidth = TestFonts.Default.MeasureTextWidth("Hello", pixelHeight);
        var expectedHeight = TestFonts.Default.LineHeightEm * pixelHeight;
        // Yoga rounds computed layout to the pixel grid (default point scale 1.0), so allow <=1px.
        // The point is the leaf is content-sized by the measure callback (not 0, not stretched to 400).
        Assert.AreEqual(expectedWidth, label.Size.X, 1.01f);
        Assert.AreEqual(expectedHeight, label.Size.Y, 1.01f);
    }

    [TestMethod]
    public void ScrollViewIsSizedButItsChildrenAreNotLaidOut()
    {
        var root = Root(Vector2.Zero, new Vector2(300, 200),
            new LayoutStyle { FlexDirection = FlexDirection.Row });
        var scroll = root.Add(new ScrollView { Layout = new LayoutStyle { FlexGrow = 1f } });
        var inner = scroll.Add(new Label(TestFonts.Default) { Position = new Vector2(17, 99) });

        YogaLayoutEngine.CalculateRoot(root, new Vector2(1280, 720));

        // The ScrollView (an ILayoutBoundary) is sized by flex...
        Assert.AreEqual(300f, scroll.Size.X, 0.01f);
        Assert.AreEqual(200f, scroll.Size.Y, 0.01f);
        // ...but the walk stops there: its scroll-managed child keeps its manual position.
        Assert.AreEqual(17f, inner.Position.X, 0.01f);
        Assert.AreEqual(99f, inner.Position.Y, 0.01f);
    }

    [TestMethod]
    public void NonParticipatingRootIsLeftUntouched()
    {
        var root = new Panel(TestFonts.Default)
        {
            Position = new Vector2(5, 5),
            Size = new Vector2(200, 100),
            // ParticipatesInLayout stays false.
        };
        var child = root.Add(new Label(TestFonts.Default) { Position = new Vector2(42, 43) });

        YogaLayoutEngine.Calculate([root], new Vector2(1280, 720));

        // Opt-out: manual position preserved.
        Assert.AreEqual(42f, child.Position.X, 0.01f);
        Assert.AreEqual(43f, child.Position.Y, 0.01f);
    }
}
