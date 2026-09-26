using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class PortalTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static LayoutStyle At(float x, float y, float w, float h) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = w,
        Height = h,
    };

    [TestMethod]
    public void PortalContentEscapesItsParentsClipAndSitsAboveEverything()
    {
        var log = new Log();
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 20, 20),
                    ClipContent = true,
                    Children = [new Portal(new Box { Layout = At(100, 100, 50, 50), OnPointerDown = _ => log.Add("portal") })],
                },
                new Box { Layout = At(90, 90, 100, 100), OnPointerDown = _ => log.Add("app") },
            ],
        });
        root.Update(Viewport);

        root.PointerDown(new Vector2(110, 110));
        root.PointerDown(new Vector2(170, 170));

        CollectionAssert.AreEqual(new[] { "portal", "app" }, log.Entries);
    }

    [TestMethod]
    public void EventsInAPortalBubbleToTheElementsThatRenderedIt()
    {
        var log = new Log();
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnKeyDown = e => log.Add($"owner {e.Key}"),
            Children = [new Portal(new Box { Layout = At(100, 100, 50, 50), Focusable = true })],
        });
        root.Update(Viewport);
        root.MoveFocus(forward: true);

        root.KeyDown(KeyCode.Escape);

        CollectionAssert.AreEqual(new[] { "owner Escape" }, log.Entries);
    }

    [TestMethod]
    public void AnEmptyPortalLayerLetsThePointerThrough()
    {
        var log = new Log();
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnPointerDown = _ => log.Add("app"),
            Children = [new Portal(new Box { Layout = At(0, 0, 10, 10) })],
        });
        root.Update(Viewport);

        root.PointerDown(new Vector2(200, 200));

        CollectionAssert.AreEqual(new[] { "app" }, log.Entries);
    }

    [TestMethod]
    public void RemovingAPortalRemovesItsLayer()
    {
        Element Tree(bool open) => new Box { Children = [open ? new Portal(new Box()) : null] };
        using var root = new UIRoot(Tree(true));
        root.Update(Viewport);
        Assert.AreEqual(2, root.RootRenderNode.Children.Count);

        root.SetRoot(Tree(false));
        root.Update(Viewport);

        Assert.AreEqual(1, root.RootRenderNode.Children.Count);
    }

    [TestMethod]
    public void LaterPortalsAreAboveEarlierOnes()
    {
        var log = new Log();
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Portal(new Box { Layout = At(0, 0, 50, 50), OnPointerDown = _ => log.Add("first") }),
                new Portal(new Box { Layout = At(0, 0, 50, 50), OnPointerDown = _ => log.Add("second") }),
            ],
        });
        root.Update(Viewport);

        root.PointerDown(new Vector2(10, 10));

        CollectionAssert.AreEqual(new[] { "second" }, log.Entries);
    }

    [TestMethod]
    public void AnElementRefGivesBoundsInRootCoordinatesAndCanFocus()
    {
        var anchor = new ElementRef();
        var focused = new Log();
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(10) },
            Children =
            [
                new Box { Layout = new LayoutStyle { Height = 30 } },
                new Box { Ref = anchor, Focusable = true, Layout = new LayoutStyle { Height = 20 }, OnFocus = _ => focused.Add("focus") },
            ],
        });
        root.Update(Viewport);

        Assert.AreEqual(new System.Drawing.RectangleF(10, 40, 380, 20), anchor.Bounds);
        anchor.Focus();
        Assert.IsTrue(anchor.IsFocused);
        CollectionAssert.AreEqual(new[] { "focus" }, focused.Entries);
    }

    [TestMethod]
    public void AnElementRefEmptiesWhenItsBoxGoes()
    {
        var anchor = new ElementRef();
        Element Tree(bool show) => new Box { Children = [show ? new Box { Ref = anchor } : null] };
        using var root = new UIRoot(Tree(true));
        root.Update(Viewport);
        Assert.IsTrue(anchor.IsMounted);

        root.SetRoot(Tree(false));
        root.Update(Viewport);

        Assert.IsFalse(anchor.IsMounted);
    }
}
