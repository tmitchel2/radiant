using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class VirtualListTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 5; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    // The rows' texts, top to bottom, with their tops in the root.
    private static (string Text, float Top)[] Rows(UIRoot root) =>
        [.. All(root.GetSemantics()).Where(n => n.Role == SemanticsRole.Text && n.Label!.StartsWith("Row ", System.StringComparison.Ordinal))
            .Select(n => (n.Label!, n.Bounds.Y)).OrderBy(r => r.Y)];

    private static VirtualList Numbers(int count, ScrollController controller) =>
        new(count, 30, i => new SurfaceText("Row " + i.ToString(CultureInfo.InvariantCulture))) { Controller = controller, Overscan = 2 };

    [TestMethod]
    public void OnlyTheRowsInViewAreBuilt()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = Mount(Numbers(1000, controller));

        var rows = Rows(root);

        // 300 px shows 10 rows of 30; two more are built below.
        Assert.AreEqual(12, rows.Length, string.Join(", ", rows.Select(r => r.Text)));
        Assert.AreEqual(("Row 0", 0f), rows[0]);
        Assert.AreEqual(30000f, controller.ContentSize.Y, "the list is as tall as every row");
    }

    [TestMethod]
    public void ScrollingBuildsTheRowsThatComeIntoView()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = Mount(Numbers(1000, controller));

        controller.ScrollTo(new Vector2(0, 3015), animated: false);
        Settle(root);
        var rows = Rows(root);

        Assert.AreEqual("Row 98", rows[0].Text, "two rows of overscan above row 100");
        var hundred = rows.Single(r => r.Text == "Row 100");
        Assert.AreEqual(-15f, hundred.Top, "row 100 starts 15 px above the view");
    }

    [TestMethod]
    public void AMillionRowsStayAlignedAtTheFarEnd()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        var watch = Stopwatch.StartNew();
        using var root = Mount(Numbers(1_000_000, controller));
        var mounted = watch.Elapsed;

        controller.ScrollTo(new Vector2(0, controller.MaxOffset.Y), animated: false);
        Settle(root);
        var rows = Rows(root);

        Assert.AreEqual("Row 999999", rows[^1].Text);
        for (var i = 1; i < rows.Length; i++)
        {
            Assert.AreEqual(30f, rows[i].Top - rows[i - 1].Top, $"rows {rows[i - 1].Text} and {rows[i].Text}");
        }
        Assert.AreEqual(300f, rows[^1].Top + 30f, 0.01f, "the last row ends at the bottom of the view");
        Assert.IsTrue(mounted.TotalMilliseconds < 500, $"mounting took {mounted.TotalMilliseconds} ms");
    }

    [TestMethod]
    public void AResizedViewBuildsMoreRows()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = Mount(Numbers(1000, controller));

        for (var i = 0; i < 5; i++)
        {
            root.Update(new Vector2(400, 600));
        }

        Assert.AreEqual(22, Rows(root).Length);
    }

    [TestMethod]
    public void ScrollingToAnIndexMovesAsLittleAsNeeded()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = Mount(Numbers(1000, controller));

        VirtualList.ScrollToIndex(controller, 5, 30);
        var inView = controller.Offset.Y;
        VirtualList.ScrollToIndex(controller, 50, 30);
        var below = controller.Offset.Y;
        VirtualList.ScrollToIndex(controller, 20, 30);

        Assert.AreEqual(0f, inView);
        Assert.AreEqual(51 * 30 - 300, below, "row 50's bottom meets the view's");
        Assert.AreEqual(600f, controller.Offset.Y, "row 20's top meets the view's");
    }

    [TestMethod]
    public void AnEmptyListBuildsNothing()
    {
        using var root = Mount(new VirtualList(0, 30, i => new SurfaceText("Row " + i.ToString(CultureInfo.InvariantCulture))));

        Assert.AreEqual(0, Rows(root).Length);
    }
}
