using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class AccordionTests
{
    private static readonly Vector2 Viewport = new(500, 600);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 20; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode Header(UIRoot root, string title) => All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.Button && n.Label == title);

    private static bool Shows(UIRoot root, string text) => All(root.GetSemantics()).Any(n => n.Label == text);

    private static void Click(UIRoot root, SemanticsNode node)
    {
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static AccordionItem[] Faq =>
    [
        new("Shipping", new SurfaceText("Two to four days.")),
        new("Returns", new SurfaceText("Within thirty days.")) { Subtitle = "Free for members" },
        new("Warranty", new SurfaceText("Two years.")) { Disabled = true },
        new("Contact", new SurfaceText("Write to us.")),
    ];

    [TestMethod]
    public void OneSectionOpensAtATime()
    {
        using var root = Mount(new Accordion(Faq));

        Click(root, Header(root, "Shipping"));
        var first = Shows(root, "Two to four days.");
        Click(root, Header(root, "Returns"));

        Assert.IsTrue(first);
        Assert.IsFalse(Shows(root, "Two to four days."), "opening another closed it");
        Assert.IsTrue(Shows(root, "Within thirty days."));
        Assert.AreEqual(true, Header(root, "Returns").Semantics.Expanded);
        Assert.AreEqual(false, Header(root, "Shipping").Semantics.Expanded);
    }

    [TestMethod]
    public void WithMultipleSeveralStayOpenAndAPressCloses()
    {
        using var root = Mount(new Accordion(Faq) { Multiple = true, InitiallyOpen = new HashSet<int> { 0 } });

        Click(root, Header(root, "Returns"));
        var both = Shows(root, "Two to four days.") && Shows(root, "Within thirty days.");
        Click(root, Header(root, "Shipping"));

        Assert.IsTrue(both);
        Assert.IsFalse(Shows(root, "Two to four days."));
    }

    [TestMethod]
    public void ADisabledSectionDoesNotOpen()
    {
        using var root = Mount(new Accordion(Faq));

        Click(root, Header(root, "Warranty"));

        Assert.IsFalse(Shows(root, "Two years."));
    }

    [TestMethod]
    public void ArrowsMoveBetweenHeadersSkippingDisabledOnes()
    {
        using var root = Mount(new Accordion(Faq));
        Click(root, Header(root, "Returns"));

        root.KeyDown(KeyCode.Down);
        Settle(root);
        var skipped = Header(root, "Contact").IsFocused;
        root.KeyDown(KeyCode.Home);
        Settle(root);

        Assert.IsTrue(skipped, "Down from Returns passes the disabled Warranty");
        Assert.IsTrue(Header(root, "Shipping").IsFocused);
    }

    [TestMethod]
    public void AnOwnedOpenSetIsReportedNotKept()
    {
        var reported = new List<IReadOnlySet<int>>();
        using var root = Mount(new Accordion(Faq) { Open = new HashSet<int>(), OnOpenChange = reported.Add });

        Click(root, Header(root, "Contact"));

        CollectionAssert.AreEquivalent(new[] { 3 }, reported.Single().ToArray());
        Assert.IsFalse(Shows(root, "Write to us."));
    }
}
