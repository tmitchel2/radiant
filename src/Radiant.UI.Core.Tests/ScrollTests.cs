using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Scrolling;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ScrollTests
{
    private static readonly Vector2 Viewport = new(200, 100);

    private static Element Rows(int count, Log? log = null, ScrollController? controller = null, ScrollBehaviour? behaviour = null) => new ScrollArea
    {
        Layout = new LayoutStyle { FlexGrow = 1 },
        Controller = controller,
        Behaviour = behaviour ?? new ScrollBehaviour(),
        Children = Enumerable.Range(0, count).Select(i => (Element?)new Box
        {
            Key = i,
            Layout = new LayoutStyle { Height = 20 },
            OnClick = _ => log?.Add($"row {i}"),
        }).ToArray(),
    };

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 200 && root.NeedsUpdate; i++)
        {
            root.Advance(1.0 / 60);
            root.Update(Viewport);
        }
    }

    [TestMethod]
    public void ContentLongerThanTheViewportIsLaidOutInFull()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(Rows(10, controller: controller));

        root.Update(Viewport);

        Assert.AreEqual(new Vector2(200, 200), controller.ContentSize);
        Assert.AreEqual(new Vector2(200, 100), controller.ViewportSize);
        Assert.IsTrue(controller.CanScrollVertical);
    }

    [TestMethod]
    public void TheWheelScrollsAndClicksLandOnTheScrolledContent()
    {
        var log = new Log();
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(Rows(10, log, controller));
        root.Update(Viewport);

        root.Wheel(new Vector2(50, 50), new Vector2(0, 40));
        Settle(root);
        root.PointerDown(new Vector2(50, 5));
        root.PointerUp(new Vector2(50, 5));

        Assert.AreEqual(40, controller.Offset.Y, 0.5f);
        CollectionAssert.AreEqual(new[] { "row 2" }, log.Entries);
    }

    [TestMethod]
    public void ScrollingStopsAtTheEnd()
    {
        var controller = new ScrollController(new ScrollBehaviour { Overscroll = OverscrollMode.Clamp });
        using var root = new UIRoot(Rows(10, controller: controller, behaviour: controller.Behaviour));
        root.Update(Viewport);

        root.Wheel(new Vector2(50, 50), new Vector2(0, 1000));
        Settle(root);

        Assert.AreEqual(100, controller.Offset.Y, 0.5f);
    }

    [TestMethod]
    public void AnAreaThatCannotScrollThatWayPassesTheWheelOut()
    {
        var outer = new ScrollController(new ScrollBehaviour());
        var inner = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(new ScrollArea
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Controller = outer,
            Children =
            [
                new ScrollArea
                {
                    Layout = new LayoutStyle { Height = 50 },
                    Controller = inner,
                    Children = [new Box { Layout = new LayoutStyle { Height = 80 } }],
                },
                new Box { Layout = new LayoutStyle { Height = 300 } },
            ],
        });
        root.Update(Viewport);

        root.Wheel(new Vector2(50, 10), new Vector2(0, 30));
        Settle(root);
        Assert.AreEqual(30, inner.Offset.Y, 0.5f, "the inner area scrolls first");
        Assert.AreEqual(0, outer.Offset.Y, 0.5f);

        // Scrolled to its end (30 of 30), the inner area lets the outer one scroll.
        root.Wheel(new Vector2(50, 10), new Vector2(0, 20));
        Settle(root);
        Assert.AreEqual(30, inner.Offset.Y, 0.5f);
        Assert.AreEqual(20, outer.Offset.Y, 0.5f);
    }

    [TestMethod]
    public void TheScrollPositionSurvivesRebuilds()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(Rows(10, controller: controller));
        root.Update(Viewport);
        root.Wheel(new Vector2(50, 50), new Vector2(0, 40));
        Settle(root);

        root.SetRoot(Rows(12, controller: controller));
        root.Update(Viewport);

        Assert.AreEqual(40, controller.Offset.Y, 0.5f);
        Assert.AreEqual(240, controller.ContentSize.Y);
    }

    [TestMethod]
    public void AnAreaKeepsItsOwnPositionWithoutAController()
    {
        var log = new Log();
        using var root = new UIRoot(Rows(10, log));
        root.Update(Viewport);

        root.Wheel(new Vector2(50, 50), new Vector2(0, 60));
        Settle(root);
        root.SetRoot(Rows(10, log));
        root.Update(Viewport);
        root.PointerDown(new Vector2(50, 1));
        root.PointerUp(new Vector2(50, 1));

        CollectionAssert.AreEqual(new[] { "row 3" }, log.Entries);
    }
}
