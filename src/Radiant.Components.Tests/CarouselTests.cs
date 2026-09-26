using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class CarouselTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { Width = 400, Padding = Edges.All(0) }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root, int frames = 180)
    {
        for (var i = 0; i < frames; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static string? Current(UIRoot root) => All(root).Single(n => n.Label == "Photos").Semantics.Value;

    private static void Click(UIRoot root, string label)
    {
        var node = All(root).Single(n => n.Role == SemanticsRole.Button && n.Label == label);
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static Carousel Photos => new([.. Enumerable.Range(1, 4).Select(i => (Element?)new SurfaceText($"Photo {i}"))]) { Label = "Photos", Height = 120 };

    // The slide whose left edge is at the carousel's.
    private static string Showing(UIRoot root)
    {
        var left = All(root).Single(n => n.Label == "Photos").Bounds.X;
        return All(root).Single(n => n.Role == SemanticsRole.Group && n.Label?.StartsWith("Slide", System.StringComparison.Ordinal) == true && System.MathF.Abs(n.Bounds.X - left) < 1).Label!;
    }

    [TestMethod]
    public void TheButtonsTurnASlideAtATime()
    {
        using var root = Mount(Photos);
        var first = All(root).Any(n => n.Label == "Previous slide");

        Click(root, "Next slide");
        Click(root, "Next slide");
        var third = Showing(root);
        Click(root, "Previous slide");

        Assert.IsFalse(first, "nothing before the first slide");
        Assert.AreEqual("Slide 3 of 4", third);
        Assert.AreEqual("2 of 4", Current(root));
    }

    [TestMethod]
    public void TheDotsGoStraightToASlide()
    {
        using var root = Mount(Photos);

        Click(root, "Go to slide 4");

        Assert.AreEqual("Slide 4 of 4", Showing(root));
        Assert.IsFalse(All(root).Any(n => n.Label == "Next slide"), "nothing after the last");
    }

    [TestMethod]
    public void AfterAWheelScrollItSettlesOnTheNearestSlide()
    {
        using var root = Mount(Photos);

        // 400 px slides with 16 px gaps: 500 px along is nearer the second slide (416) than the third (832).
        root.Wheel(new Vector2(200, 60), new Vector2(500, 0));
        Settle(root, 240);

        Assert.AreEqual("Slide 2 of 4", Showing(root));
    }

    [TestMethod]
    public void ArrowsTurnSlidesWithFocusInside()
    {
        using var root = Mount(Photos);
        Click(root, "Next slide");

        root.KeyDown(KeyCode.Right);
        Settle(root);

        Assert.AreEqual("3 of 4", Current(root));
    }
}
