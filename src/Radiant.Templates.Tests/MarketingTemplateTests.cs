using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Components;
using Radiant.UI.Core;
using static Radiant.Templates.Tests.TemplateHarness;

namespace Radiant.Templates.Tests;

[TestClass]
public class MarketingTemplateTests
{
    [TestMethod]
    public void AHeroShowsItsTextAndActions()
    {
        var started = 0;
        using var root = Mount(new Hero("Desktop apps, built in C#")
        {
            Eyebrow = "Radiant 1.0",
            Text = "A declarative UI.",
            Actions = [new SurfaceButton("Get started") { OnPress = () => started++ }, new SurfaceButton("Read the docs", ButtonVariant.Text)],
            Picture = Picture,
        });

        Click(root, Find(root, SemanticsRole.Button, "Get started"));

        Assert.AreEqual(1, started);
        Assert.IsTrue(Shows(root, "Radiant 1.0") && Shows(root, "Desktop apps, built in C#") && Shows(root, "A declarative UI."));
    }

    [TestMethod]
    public void ChoosingAPlanReportsWhichOne()
    {
        var chosen = new List<int>();
        using var root = Mount(new PricingTiers([
            new PricingTier("Hobby", "$0", ["One app"]),
            new PricingTier("Team", "$12", ["Unlimited apps", "Themes"]) { Featured = true },
        ], chosen.Add));

        Click(root, Find(root, SemanticsRole.Button, "Choose plan"));
        Click(root, Find(root, SemanticsRole.Button, "Get started"));

        CollectionAssert.AreEqual(new[] { 0, 1 }, chosen);
        Assert.IsTrue(Shows(root, "Most popular"), "the featured plan is marked");
        Assert.IsTrue(Shows(root, "Themes"));
    }

    [TestMethod]
    public void FeaturesFillEqualColumns()
    {
        using var root = Mount(new FeatureGrid("Everything", [
            new Feature("palette", "Themes", "One colour."),
            new Feature("bolt", "Fast", "Only what changed."),
            new Feature("widgets", "Components", "All of them."),
            new Feature("text_fields", "Text", "Done properly."),
        ]));

        var titles = new[] { "Themes", "Fast", "Components", "Text" }.Select(t => All(root).First(n => n.Label == t).Bounds).ToArray();

        Assert.AreEqual(titles[0].X, titles[3].X, 0.5f, "the fourth wraps under the first");
        Assert.AreEqual(titles[1].X - titles[0].X, titles[2].X - titles[1].X, 1.5f, "columns are evenly spaced");
    }
}
