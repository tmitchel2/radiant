using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class GridTests
{
    private static RenderNode Only(UIRoot root) => root.RootRenderNode.Children.Single();

    private static Box Cell(float height = 20) => new() { Layout = new LayoutStyle { Height = height } };

    [TestMethod]
    public void AFixedCountSplitsTheWidthIntoEqualColumns()
    {
        using var root = new UIRoot(new Grid { Columns = 3, ColumnGap = 20, RowGap = 10, Children = [Cell(), Cell(), Cell(), Cell()] });

        root.Update(new Vector2(400, 300));

        var cells = Only(root).Children;
        Assert.AreEqual(120f, cells[0].Size.X, 1f);
        CollectionAssert.AreEqual(new[] { 0f, 140f, 280f }, cells.Take(3).Select(c => System.MathF.Round(c.Position.X)).ToArray());
        Assert.AreEqual(new Vector2(0, 30), cells[3].Position, "wraps to a second row, below the gap");
        Assert.AreEqual(cells[0].Size.X, cells[3].Size.X, 0.5f, "a short last row keeps the column width");
    }

    [TestMethod]
    public void WithoutACountAsManyColumnsFitAsTheMinimumWidthAllows()
    {
        Grid Build() => new() { MinColumnWidth = 150, ColumnGap = 10, Children = [Cell(), Cell(), Cell(), Cell(), Cell()] };
        using var root = new UIRoot(Build());

        root.Update(new Vector2(500, 300));
        var wide = Only(root).Children.Count(c => c.Position.Y == 0);
        root.Update(new Vector2(320, 300));
        var narrow = Only(root).Children.Count(c => c.Position.Y == 0);

        Assert.AreEqual(3, wide, "(500 + 10) / (150 + 10) = 3");
        Assert.AreEqual(2, narrow);
        Assert.AreEqual(155f, Only(root).Children[0].Size.X, 1f);
    }

    [TestMethod]
    public void MaxColumnsCapsTheCount()
    {
        using var root = new UIRoot(new Grid { MinColumnWidth = 50, MaxColumns = 2, Children = [Cell(), Cell(), Cell()] });

        root.Update(new Vector2(400, 300));

        Assert.AreEqual(200f, Only(root).Children[0].Size.X, 1f);
        Assert.AreEqual(20f, Only(root).Children[2].Position.Y);
    }

    [TestMethod]
    public void CellsInARowAreAsTallAsTheTallest()
    {
        using var root = new UIRoot(new Grid { Columns = 2, Children = [new Box(), new Box { Layout = new LayoutStyle { MinHeight = 50 } }] });

        root.Update(new Vector2(400, 300));

        Assert.AreEqual(50f, Only(root).Children[0].Size.Y);
    }

    [TestMethod]
    public void AChildsOwnWidthIsOverriddenEvenAfterItRebuilds()
    {
        var width = new Signal<float>(30);
        using var root = new UIRoot(new Grid { Columns = 2, Children = [new SizedCell(width), Cell()] });
        root.Update(new Vector2(400, 300));

        width.Value = 90;
        root.Update(new Vector2(400, 300));

        Assert.AreEqual(200f, Only(root).Children[0].Size.X, 1f);
    }

    [TestMethod]
    public void AddedCellsTakeTheColumnWidth()
    {
        var count = new Signal<int>(1);
        using var root = new UIRoot(new Cells(count));
        root.Update(new Vector2(400, 300));

        count.Value = 3;
        root.Update(new Vector2(400, 300));

        var cells = Only(root).Children;
        Assert.AreEqual(3, cells.Count);
        Assert.IsTrue(cells.All(c => System.MathF.Abs(c.Size.X - 100f) < 1f), string.Join(", ", cells.Select(c => c.Size.X)));
    }

    private sealed record SizedCell(Signal<float> Width) : Component
    {
        public override Element? Build(BuildContext context) =>
            new Box { Layout = new LayoutStyle { Width = context.Watch(Width), Height = 20 } };
    }

    private sealed record Cells(Signal<int> Count) : Component
    {
        public override Element? Build(BuildContext context) =>
            new Grid { Columns = 4, Children = [.. Enumerable.Range(0, context.Watch(Count)).Select(_ => (Element?)Cell())] };
    }
}
