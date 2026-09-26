using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class TitleBarTests
{
    private static readonly Vector2 Viewport = new(800, 400);

    private static UIRoot Mount(Element element, HeadlessPlatform platform)
    {
        var root = new UIRoot(PlatformContext.Platform.Provide(platform, new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] })));
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

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static HeadlessPlatform Mac => new() { Chrome = { IsSupported = true, LeadingInset = 72, TitleBarHeight = 28 } };

    private static TitleBar Bar(List<string> pressed) => new("Untitled") { Leading = [new IconButton("view_sidebar", "Toggle sidebar") { OnPress = () => pressed.Add("sidebar") }] };

    [TestMethod]
    public void TheBarTakesOverTheTitleBarAndKeepsClearOfTheWindowControls()
    {
        var platform = Mac;
        using var root = Mount(Bar([]), platform);

        var toggle = All(root).Single(n => n.Label == "Toggle sidebar").Bounds;

        Assert.IsTrue(platform.Chrome.ExtendsIntoTitleBar);
        Assert.IsTrue(toggle.X >= 72, $"the first control starts past the traffic lights ({toggle.X})");
        Assert.AreEqual(38f, All(root).Single(n => n.Label == "Title bar").Bounds.Height);
    }

    [TestMethod]
    public void PressingTheEmptyBarMovesTheWindowAndADoubleClickZoomsIt()
    {
        var platform = Mac;
        using var root = Mount(Bar([]), platform);

        root.PointerDown(new Vector2(500, 15));
        root.PointerUp(new Vector2(500, 15));
        root.PointerDown(new Vector2(500, 15));
        root.PointerUp(new Vector2(500, 15));
        var title = All(root).Single(n => n.Role == SemanticsRole.Heading && n.Label == "Untitled").Bounds;
        root.PointerDown(new Vector2(title.X + 5, title.Y + 5));

        Assert.AreEqual(2, platform.Chrome.DragCount, "the empty bar, then the title");
        Assert.AreEqual(1, platform.Chrome.DoubleClickCount);
    }

    [TestMethod]
    public void ItsButtonsPressWithoutMovingTheWindow()
    {
        var platform = Mac;
        var pressed = new List<string>();
        using var root = Mount(Bar(pressed), platform);
        var toggle = All(root).Single(n => n.Label == "Toggle sidebar").Bounds;

        root.PointerDown(new Vector2(toggle.X + 5, toggle.Y + 5));
        root.PointerUp(new Vector2(toggle.X + 5, toggle.Y + 5));

        CollectionAssert.AreEqual(new[] { "sidebar" }, pressed);
        Assert.AreEqual(0, platform.Chrome.DragCount);
    }

    [TestMethod]
    public void GoneItGivesTheTitleBarBack()
    {
        var platform = Mac;
        var shown = new Signal<bool>(true);
        using var root = Mount(new Host(ctx => ctx.Watch(shown) ? Bar([]) : null), platform);

        shown.Value = false;
        Settle(root);

        Assert.IsFalse(platform.Chrome.ExtendsIntoTitleBar);
    }

    [TestMethod]
    public void WithoutPlatformSupportItsAPlainBar()
    {
        var platform = new HeadlessPlatform();
        using var root = Mount(Bar([]), platform);

        var toggle = All(root).Single(n => n.Label == "Toggle sidebar").Bounds;

        Assert.IsFalse(platform.Chrome.ExtendsIntoTitleBar);
        Assert.IsTrue(toggle.X < 20, "no inset for window controls it doesn't cover");
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
