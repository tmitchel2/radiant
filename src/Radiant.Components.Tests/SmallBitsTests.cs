using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class SmallBitsTests
{
    private static readonly Vector2 Viewport = new(700, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart, RowGap = 40 },
            Children = [element, new SurfaceText("Elsewhere")],
        }));
        Settle(root, 5);
        return root;
    }

    private static void Settle(UIRoot root, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    [TestMethod]
    public void AGroupShowsSomeAndCountsTheRest()
    {
        using var root = Mount(new AvatarGroup(["Ada Lovelace", "Grace Hopper", "Alan Turing", "Katherine Johnson", "Edsger Dijkstra"]) { Max = 4 });

        Assert.IsTrue(All(root).Any(n => n.Label == "+2"), "three shown, two counted");
        Assert.AreEqual("Ada Lovelace, Grace Hopper, Alan Turing, Katherine Johnson, Edsger Dijkstra",
            All(root).Single(n => n.Role == SemanticsRole.Group && n.Label?.StartsWith("Ada", System.StringComparison.Ordinal) == true).Label);
    }

    private static HoverCard Profile => new(new SurfaceText("@ada"), new SurfaceText("Ada Lovelace, analyst"));

    [TestMethod]
    public void AHoverCardOpensAfterTheRestAndStaysOverTheCard()
    {
        using var root = Mount(Profile);
        var trigger = All(root).Single(n => n.Label == "@ada");

        root.PointerMove(Centre(trigger));
        Settle(root, 10);
        var early = All(root).Any(n => n.Label == "Ada Lovelace, analyst");
        Settle(root, 30);
        var card = All(root).Single(n => n.Label == "Ada Lovelace, analyst");
        root.PointerMove(Centre(card));
        Settle(root, 60);
        var kept = All(root).Any(n => n.Label == "Ada Lovelace, analyst");
        root.PointerMove(new Vector2(680, 480));
        Settle(root, 60);

        Assert.IsFalse(early, "not before the rest");
        Assert.IsTrue(kept, "the pointer moved onto the card, which stayed");
        Assert.IsFalse(All(root).Any(n => n.Label == "Ada Lovelace, analyst"), "gone once the pointer left both");
    }

    [TestMethod]
    public void ACodeFillsItsBoxesIgnoringOtherCharactersAndCompletes()
    {
        var completed = new List<string>();
        using var root = Mount(new OtpInput(completed.Add));
        var field = All(root).Single(n => n.Role == SemanticsRole.TextField && n.Label == "Verification code");
        root.PointerDown(Centre(field));
        root.PointerUp(Centre(field));
        Settle(root, 2);

        root.TextInput("12a3");
        Settle(root, 2);
        var partial = All(root).Single(n => n.Role == SemanticsRole.TextField).Semantics.Value;
        root.TextInput("45 6789");
        Settle(root, 2);

        Assert.AreEqual("123", partial);
        CollectionAssert.AreEqual(new[] { "123456" }, completed, "six digits, the rest dropped");
        Assert.IsTrue(All(root).Any(n => n.Label == "6"), "each digit shows in its box");
    }
}
