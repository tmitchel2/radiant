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
public class ChartTests
{
    private static readonly Vector2 Viewport = new(800, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20) },
            Children = [element],
        }));
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

    [TestMethod]
    public void ScalesTickAtRoundNumbers()
    {
        Assert.AreEqual(new ChartScale(0, 80, 20), ChartScale.Nice(24, 61));
        Assert.AreEqual(new ChartScale(20, 65, 5), ChartScale.Nice(24, 61, ticks: 10, fromZero: false));
        Assert.AreEqual(new ChartScale(-50, 150, 50), ChartScale.Nice(-30, 120));
        CollectionAssert.AreEqual(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, ChartScale.Nice(0, 0.9).Ticks().ToArray());
        Assert.AreEqual(new ChartScale(0, 1, 0.25), ChartScale.Nice(0, 0), "flat data still gets an axis");
    }

    private static LineChart Revenue => new(["Jan", "Feb", "Mar"], [new ChartSeries("Sales", [10, 30, 20]), new ChartSeries("Costs", [5, 8, 12])]) { Title = "Revenue" };

    [TestMethod]
    public void AChartReadsItsValuesAsText()
    {
        using var root = Mount(Revenue);

        var chart = All(root).Single(n => n.Role == SemanticsRole.Image && n.Label == "Revenue");

        Assert.AreEqual("Revenue: Sales 10, 30, 20; Costs 5, 8, 12", chart.Semantics.Description);
    }

    [TestMethod]
    public void TheAxisAndCategoriesAreLabelled()
    {
        using var root = Mount(Revenue);

        var labels = All(root).Where(n => n.Role == SemanticsRole.Text).Select(n => n.Label).ToHashSet();

        Assert.IsTrue(new[] { "0", "10", "20", "30", "Jan", "Feb", "Mar", "Sales", "Costs" }.All(labels.Contains), string.Join(", ", labels));
    }

    [TestMethod]
    public void ThePointerShowsEachSeriesValueAtItsCategory()
    {
        using var root = Mount(Revenue);
        var chart = All(root).Single(n => n.Role == SemanticsRole.Image).Bounds;
        var feb = All(root).Single(n => n.Label == "Feb").Bounds;

        root.PointerMove(new Vector2(feb.X + feb.Width / 2, chart.Y + 120));
        Settle(root);

        var shown = All(root).Select(n => n.Label).ToList();
        Assert.IsTrue(shown.Contains("Sales  30") && shown.Contains("Costs  8"), string.Join(", ", shown));
    }

    [TestMethod]
    public void StackedBarsScaleToTheirTotals()
    {
        using var root = Mount(new BarChart(["A", "B"], [new ChartSeries("One", [300, 100]), new ChartSeries("Two", [250, 50])]) { Stacked = true });

        var labels = All(root).Where(n => n.Role == SemanticsRole.Text).Select(n => n.Label).ToHashSet();

        Assert.IsTrue(labels.Contains("600"), "550 stacked needs an axis to 600: " + string.Join(", ", labels));
    }

    [TestMethod]
    public void ADonutGivesEachPartItsShare()
    {
        using var root = Mount(new DonutChart([("Search", 48), ("Direct", 27), ("Social", 15), ("Email", 10)]) { Title = "Sources" });

        var chart = All(root).Single(n => n.Role == SemanticsRole.Image && n.Label == "Sources");

        StringAssert.Contains(chart.Semantics.Description, "Search 48 (48%)");
        Assert.IsTrue(All(root).Any(n => n.Label == "100"), "the total in the middle");
    }
}
