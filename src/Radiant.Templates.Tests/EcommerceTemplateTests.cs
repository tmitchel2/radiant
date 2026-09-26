using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.UI.Core;
using static Radiant.Templates.Tests.TemplateHarness;

namespace Radiant.Templates.Tests;

[TestClass]
public class EcommerceTemplateTests
{
    private static Product Lamp => new("Aurora lamp", "$89", Picture) { Detail = "Warm white", Rating = 4.8, Badge = "New" };

    private static Product Mug => new("Tide mug", "$24", Picture);

    [TestMethod]
    public void AddingAProductToTheCartReportsWhichOne()
    {
        var added = new List<int>();
        using var root = Mount(new ProductGrid([Lamp, Mug], added.Add));

        Click(root, Find(root, SemanticsRole.Button, "Add Tide mug to cart"));

        CollectionAssert.AreEqual(new[] { 1 }, added);
        Assert.IsTrue(Shows(root, "Warm white") && Shows(root, "4.8") && Shows(root, "New"));
    }

    [TestMethod]
    public void ProductCardsInAShortLastRowKeepTheColumnWidth()
    {
        using var root = Mount(new ProductGrid([Lamp, Mug, Lamp with { Name = "Meadow throw" }, Mug with { Name = "Dusk print" }, Lamp with { Name = "Fern pot" }], null));

        var first = Find(root, SemanticsRole.Button, "Add Aurora lamp to cart").Bounds;
        var last = Find(root, SemanticsRole.Button, "Add Fern pot to cart").Bounds;

        Assert.IsTrue(last.Y > first.Y, "the fifth card wraps");
        Assert.AreEqual(first.X, last.X, 0.5f, "and its button lines up with the first card's");
    }

    [TestMethod]
    public void TheCartTotalsItsLinesAndShipping()
    {
        using var root = Mount(new CartSummary([new CartLine(Lamp, 2, 89m), new CartLine(Mug, 1, 24m)]) { Shipping = 5m });

        Assert.IsTrue(Shows(root, "$178.00"), "a line's amount is its price times quantity");
        Assert.IsTrue(Shows(root, "$202.00"), "subtotal");
        Assert.IsTrue(Shows(root, "$5.00"));
        Assert.IsTrue(Shows(root, "$207.00"), "total");
    }

    [TestMethod]
    public void FreeShippingSaysSo()
    {
        using var root = Mount(new CartSummary([new CartLine(Mug, 1, 24m)]));

        Assert.IsTrue(Shows(root, "Free"));
    }

    [TestMethod]
    public void QuantityButtonsAskForOneMoreOrFewer()
    {
        var changes = new List<(int, int)>();
        var checkedOut = 0;
        using var root = Mount(new CartSummary([new CartLine(Lamp, 2, 89m), new CartLine(Mug, 1, 24m)])
        {
            OnQuantityChange = (index, quantity) => changes.Add((index, quantity)),
            OnCheckout = () => checkedOut++,
        });

        Click(root, All(root).Where(n => n.Role == SemanticsRole.Button && n.Label == "More").ElementAt(1));
        Click(root, All(root).First(n => n.Role == SemanticsRole.Button && n.Label == "Fewer"));
        Click(root, Find(root, SemanticsRole.Button, "Check out"));

        CollectionAssert.AreEqual(new[] { (1, 2), (0, 1) }, changes);
        Assert.AreEqual(1, checkedOut);
    }
}
