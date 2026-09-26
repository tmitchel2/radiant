using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class MoreComponentTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element],
        }));
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

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string? label = null) =>
        All(root.GetSemantics()).First(n => n.Role == role && (label is null || n.Label == label));

    private static void Click(UIRoot root, Vector2 at)
    {
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    [TestMethod]
    public void AFilterChipIsACheckBoxAndAnInputChipCanBeRemoved()
    {
        var removed = 0;
        using var filter = Mount(new Chip("Open") { Selected = true });
        using var input = Mount(new Chip("tom") { OnRemove = () => removed++ });

        Assert.AreEqual(SemanticsRole.CheckBox, Find(filter, SemanticsRole.CheckBox, "Open").Role);
        var chip = Find(input, SemanticsRole.Button, "tom");
        Click(input, new Vector2(chip.Bounds.Right - 14, chip.Bounds.Y + 16));
        Assert.AreEqual(1, removed);
    }

    [TestMethod]
    public void ASliderFollowsThePointerAndTheKeys()
    {
        var value = new UI.Core.Signal<float>(0f);
        using var root = Mount(new Host(ctx => new Box
        {
            Layout = new LayoutStyle { Width = 400 },
            Children = [new Slider(ctx.Watch(value), v => value.Value = v) { Label = "Volume" }],
        }));
        var slider = Find(root, SemanticsRole.Slider, "Volume");

        root.PointerDown(new Vector2(slider.Bounds.X + slider.Bounds.Width * 0.25f, Centre(slider).Y));
        Settle(root);
        Assert.AreEqual(0.25f, value.Value, 0.01f);
        root.PointerMove(new Vector2(slider.Bounds.X + slider.Bounds.Width * 0.75f, Centre(slider).Y));
        Settle(root);
        Assert.AreEqual(0.75f, value.Value, 0.01f);
        root.PointerUp(new Vector2(slider.Bounds.X + slider.Bounds.Width * 0.75f, Centre(slider).Y));

        root.KeyDown(KeyCode.End);
        Settle(root);
        Assert.AreEqual(1f, value.Value);
        root.KeyDown(KeyCode.Left);
        Settle(root);
        Assert.AreEqual(0.99f, value.Value, 1e-4f);
    }

    [TestMethod]
    public void ASteppedSliderSnaps()
    {
        var changes = new List<float>();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { Width = 400 },
            Children = [new Slider(0f, changes.Add) { Step = 0.25f, Label = "Stepped" }],
        });
        var slider = Find(root, SemanticsRole.Slider, "Stepped");

        root.PointerDown(new Vector2(slider.Bounds.X + slider.Bounds.Width * 0.3f, Centre(slider).Y));

        CollectionAssert.AreEqual(new[] { 0.25f }, changes);
    }

    [TestMethod]
    public void TabsSelectByPressAndArrowAndTheIndicatorFollows()
    {
        var selected = new UI.Core.Signal<int>(0);
        using var root = Mount(new Host(ctx => new Box
        {
            Layout = new LayoutStyle { Width = 480 },
            Children = [new Tabs([new Tab("One"), new Tab("Two"), new Tab("Three")], ctx.Watch(selected), i => selected.Value = i)],
        }));

        Click(root, Centre(Find(root, SemanticsRole.Tab, "Two")));
        Assert.AreEqual(1, selected.Value);
        root.KeyDown(KeyCode.Right);
        Settle(root);
        Assert.AreEqual(2, selected.Value);

        var three = Find(root, SemanticsRole.Tab, "Three");
        var indicator = FindIndicator(root);
        Assert.AreEqual(three.Bounds.X, indicator.X, 0.5f);
        Assert.AreEqual(three.Bounds.Width, indicator.Width, 0.5f);
    }

    [TestMethod]
    public void ProgressReportsItsValueAndABadgeCapsItsCount()
    {
        using var progress = Mount(new LinearProgress { Value = 0.42f, Label = "Upload" });
        using var badge = Mount(new Badge(new SurfaceIcon("mail")) { Count = 150 });

        Assert.AreEqual("42%", Find(progress, SemanticsRole.ProgressIndicator, "Upload").Semantics.Value);
        Assert.IsTrue(All(badge.GetSemantics()).Any(n => n.Label == "99+"));
    }

    [TestMethod]
    public void ACircularProgressIsAProgressIndicatorAndASpinnerHasNoValue()
    {
        using var ring = Mount(new CircularProgress { Value = 0.5f, Label = "Done" });
        using var spinner = Mount(new CircularProgress { Label = "Working" });

        Assert.AreEqual("50%", Find(ring, SemanticsRole.ProgressIndicator, "Done").Semantics.Value);
        Assert.IsNull(Find(spinner, SemanticsRole.ProgressIndicator, "Working").Semantics.Value);
        Assert.AreEqual(40, Find(ring, SemanticsRole.ProgressIndicator).Bounds.Width);
    }

    private static System.Drawing.RectangleF FindIndicator(UIRoot root)
    {
        // The indicator is the tab list's last child: a 3 px bar.
        var tabList = All(root.GetSemantics()).First(n => n.Role == SemanticsRole.TabList);
        var node = Descend(root.RootRenderNode).First(n => n is BoxRenderNode && Math.Abs(n.Size.Y - 3) < 0.01f && n.AbsolutePosition.Y > tabList.Bounds.Y);
        return new System.Drawing.RectangleF(node.AbsolutePosition.X, node.AbsolutePosition.Y, node.Size.X, node.Size.Y);

        static IEnumerable<RenderNode> Descend(RenderNode n) => n.Children.SelectMany(Descend).Prepend(n);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
