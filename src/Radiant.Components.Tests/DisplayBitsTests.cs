using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class DisplayBitsTests
{
    private static UIRoot Mount(Element element, float width)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { Width = width }, Children = [element] }));
        root.Update(new Vector2(width, 600));
        return root;
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static DescriptionList Order => new([("Order", DescriptionList.Text("#10482")), ("Placed", DescriptionList.Text("26 September"))]) { TermWidth = 120 };

    [TestMethod]
    public void DetailsSitBesideTheirTermsWhenThereIsRoom()
    {
        using var wide = Mount(Order, 600);
        using var narrow = Mount(Order, 280);

        var term = All(wide).Single(n => n.Label == "Order" && n.Role == SemanticsRole.Group).Bounds;
        var beside = All(wide).Single(n => n.Label == "#10482").Bounds;
        var under = All(narrow).Single(n => n.Label == "#10482").Bounds;
        var narrowTerm = All(narrow).Single(n => n.Label == "Order" && n.Role == SemanticsRole.Group).Bounds;

        Assert.AreEqual(term.X + 120 + 24, beside.X, 0.5f);
        Assert.IsTrue(under.Y > narrowTerm.Y + 10 && under.X == narrowTerm.X, "wrapped under its term");
    }

    [TestMethod]
    public void TimelineEventsAreReadInOrderWithTheirTimes()
    {
        using var root = Mount(new Timeline([new TimelineEvent("Placed", "Mon"), new TimelineEvent("Packed", "Tue") { Text = "Two boxes." }]), 400);

        var events = All(root).Where(n => n.Role == SemanticsRole.ListItem).ToArray();

        CollectionAssert.AreEqual(new[] { "Placed", "Packed" }, events.Select(e => e.Label).ToArray());
        Assert.AreEqual(("Tue", "Two boxes."), (events[1].Semantics.Value, events[1].Semantics.Description));
    }
}
