using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Text;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class RightToLeftTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Directionality(TextDirection.RightToLeft, new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element],
        })));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        root.Update(Viewport);
        for (var i = 0; i < 40; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<RenderNode> All(RenderNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string? label = null) =>
        All(root.GetSemantics()).First(n => n.Role == role && (label is null || n.Label == label));

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    [TestMethod]
    public void ContentStartsAtTheRight()
    {
        using var root = Mount(new SurfaceButton("Save"));

        var button = Find(root, SemanticsRole.Button, "Save");

        Assert.AreEqual(Viewport.X - 20, button.Bounds.Right, 0.5f);
    }

    [TestMethod]
    public void ASliderRunsFromTheRightAndItsArrowsFollow()
    {
        var value = new Signal<float>(0f);
        using var root = Mount(new Host(ctx => new Box
        {
            Layout = new LayoutStyle { Width = 400 },
            Children = [new Slider(ctx.Watch(value), v => value.Value = v) { Label = "Volume" }],
        }));
        var slider = Find(root, SemanticsRole.Slider, "Volume");

        // A quarter of the way from the left is three quarters of the way from the start.
        var x = slider.Bounds.X + 10 + (slider.Bounds.Width - 20) * 0.25f;
        root.PointerDown(new Vector2(x, Centre(slider).Y));
        root.PointerUp(new Vector2(x, Centre(slider).Y));
        Settle(root);
        Assert.AreEqual(0.75f, value.Value, 0.01f);

        Press(root, KeyCode.Left);
        Assert.AreEqual(0.76f, value.Value, 0.01f);
        Press(root, KeyCode.Right);
        Press(root, KeyCode.Right);
        Assert.AreEqual(0.74f, value.Value, 0.01f);
    }

    [TestMethod]
    public void TabsRunFromTheRightAndLeftChoosesTheNext()
    {
        var selected = new Signal<int>(0);
        using var root = Mount(new Host(ctx => new Box
        {
            Layout = new LayoutStyle { Width = 480 },
            Children = [new Tabs([new Tab("One"), new Tab("Two"), new Tab("Three")], ctx.Watch(selected), i => selected.Value = i)],
        }));
        var (one, two) = (Find(root, SemanticsRole.Tab, "One"), Find(root, SemanticsRole.Tab, "Two"));
        Assert.IsTrue(one.Bounds.X > two.Bounds.X);

        root.PointerDown(Centre(two));
        root.PointerUp(Centre(two));
        Settle(root);
        Press(root, KeyCode.Left);

        Assert.AreEqual(2, selected.Value);
        // The indicator sits under the chosen tab.
        var three = Find(root, SemanticsRole.Tab, "Three");
        var tabList = Find(root, SemanticsRole.TabList);
        var indicator = All(root.RootRenderNode).First(n => n is BoxRenderNode && Math.Abs(n.Size.Y - 3) < 0.01f && n.AbsolutePosition.Y > tabList.Bounds.Y);
        Assert.AreEqual(three.Bounds.X, indicator.AbsolutePosition.X, 0.5f);
    }

    [TestMethod]
    public void BackAndForwardIconsMirror()
    {
        using var root = Mount(new Box
        {
            Children =
            [
                new SurfaceIcon("arrow_back"),
                new SurfaceIcon("chevron_right"),
                new SurfaceIcon("chevron_left") { MirrorInRightToLeft = false },
                new SurfaceIcon("search"),
            ],
        });

        var glyphs = All(root.RootRenderNode).OfType<TextRenderNode>().Select(t => t.Element.Text).ToArray();

        CollectionAssert.AreEqual(new[] { "arrow_forward", "chevron_left", "chevron_left", "search" }, glyphs);
    }

    [TestMethod]
    public void AnchoredContentLinesUpWithTheAnchorsRightEdge()
    {
        var anchor = new ElementRef();
        var content = new ElementRef();
        using var root = Mount(new Box
        {
            Children =
            [
                new Box { Ref = anchor, Layout = new LayoutStyle { Width = 100, Height = 30 } },
                new Anchored(anchor, new Box { Ref = content, Layout = new LayoutStyle { Width = 200, Height = 50 } }),
            ],
        });

        Assert.AreEqual(anchor.Bounds.Right, content.Bounds.Right, 0.5f);
        Assert.AreEqual(anchor.Bounds.Bottom + 4, content.Bounds.Y, 0.5f);
    }

    [TestMethod]
    public void ASplitterDraggedLeftwardsGrowsTheFirstPane()
    {
        var size = new Signal<float>(200f);
        using var root = Mount(new Host(ctx => new Box
        {
            Layout = new LayoutStyle { Width = 500, Height = 200 },
            Children =
            [
                new Splitter(new Box(), new Box())
                {
                    Size = ctx.Watch(size),
                    OnSizeChange = s => size.Value = s,
                    Label = "Sidebar",
                    Layout = new LayoutStyle { FlexGrow = 1, AlignSelf = Align.Stretch },
                },
            ],
        }));
        var handle = Find(root, SemanticsRole.Separator, "Sidebar");

        root.PointerDown(Centre(handle));
        root.PointerMove(Centre(handle) - new Vector2(30, 0));
        root.PointerUp(Centre(handle) - new Vector2(30, 0));
        Settle(root);

        Assert.AreEqual(230f, size.Value, 0.5f);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
