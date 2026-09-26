using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class PerformanceTests
{
    // A leaf with its own state, so it can be rebuilt alone.
    private sealed record Leaf(int Id) : Component
    {
        public static State<int>? Last { get; set; }

        public override Element? Build(BuildContext context)
        {
            var state = context.UseState(0);
            if (Id == 0)
            {
                Last = state;
            }
            return new Box();
        }
    }

    [TestMethod]
    public void RebuildingOneLeafOfAFiveThousandNodeTreeIsCheap()
    {
        // 50 rows of 100 leaves: 5,000 components and 5,000 boxes under them.
        var tree = new Box
        {
            Children = Enumerable.Range(0, 50).Select(row => (Element?)new Box
            {
                Key = row,
                Children = Enumerable.Range(0, 100).Select(i => (Element?)new Leaf(row * 100 + i) { Key = i }).ToArray(),
            }).ToArray(),
        };
        using var root = new UIRoot(tree);
        root.Update(new Vector2(1000, 1000));

        const int runs = 200;
        for (var i = 0; i < 20; i++)
        {
            Leaf.Last!.Set(i + 1);
            root.Update(new Vector2(1000, 1000));
        }
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < runs; i++)
        {
            Leaf.Last!.Set(1000 + i);
            root.Update(new Vector2(1000, 1000));
        }
        var perRebuild = watch.Elapsed.TotalMilliseconds / runs;

        // The target is 0.5 ms in Release; Debug builds and shared test machines get headroom.
        Assert.IsTrue(perRebuild < 2.0, $"{perRebuild:0.000} ms per rebuild and relayout");
        System.Console.WriteLine($"One leaf rebuilt and relaid out in a 5k-node tree: {perRebuild:0.000} ms");
    }
}
