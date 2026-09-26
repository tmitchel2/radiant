using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ReconcilerTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(element);
        root.Update(Viewport);
        return root;
    }

    [TestMethod]
    public void HostElementsBecomeARenderTree()
    {
        using var root = Mount(new Box { Children = [new TextBlock("a"), new Box()] });

        var box = root.RootRenderNode.Children.Single();
        Assert.IsInstanceOfType<BoxRenderNode>(box);
        Assert.IsInstanceOfType<TextRenderNode>(box.Children[0]);
        Assert.IsInstanceOfType<BoxRenderNode>(box.Children[1]);
    }

    [TestMethod]
    public void ComponentsLeaveNoTraceInTheRenderTree()
    {
        var log = new Log();
        using var root = Mount(new Box { Children = [new Counter("a", log), new Counter("b", log)] });

        var box = root.RootRenderNode.Children.Single();
        Assert.AreEqual(2, box.Children.Count);
        Assert.IsTrue(box.Children.All(c => c is BoxRenderNode));
    }

    [TestMethod]
    public void FragmentsFlattenIntoTheirHost()
    {
        using var root = Mount(new Box { Children = [new Fragment(new Box(), new Box()), new Box()] });

        Assert.AreEqual(3, root.RootRenderNode.Children.Single().Children.Count);
    }

    [TestMethod]
    public void SettingStateRebuildsOnlyThatComponent()
    {
        var log = new Log();
        using var root = Mount(new Box { Children = [new Counter("a", log), new Counter("b", log)] });
        log.Clear();

        Counter.Handles["a"].Count.Set(1);
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "build a" }, log.Entries);
    }

    [TestMethod]
    public void SettingStateSeveralTimesRebuildsOnce()
    {
        var log = new Log();
        using var root = Mount(new Counter("a", log));
        log.Clear();

        var count = Counter.Handles["a"].Count;
        count.Set(1);
        count.Set(2);
        count.Update(n => n + 1);
        root.Update(Viewport);

        Assert.AreEqual(1, log.Count("build a"));
        Assert.AreEqual(3, count.Value);
    }

    [TestMethod]
    public void SettingAnEqualValueDoesNotRebuild()
    {
        var log = new Log();
        using var root = Mount(new Counter("a", log));
        log.Clear();

        Counter.Handles["a"].Count.Set(0);

        Assert.IsFalse(root.NeedsUpdate);
    }

    [TestMethod]
    public void AComponentGivenEqualPropsIsNotRebuilt()
    {
        var log = new Log();
        var outer = new Log();
        Element Tree(int tick) => new Lambda(ctx =>
        {
            ctx.UseState(tick);
            outer.Add("outer");
            return new Box { Children = [new Counter("a", log)] };
        });
        using var root = Mount(Tree(0));
        log.Clear();

        root.SetRoot(Tree(1));
        root.Update(Viewport);

        Assert.AreEqual(0, log.Count("build a"));
    }

    [TestMethod]
    public void KeyedChildrenKeepTheirStateWhenReordered()
    {
        var log = new Log();
        using var root = Mount(new CounterList(["a", "b", "c"], log));
        Counter.Handles["a"].Count.Set(10);
        Counter.Handles["c"].Count.Set(30);
        root.Update(Viewport);
        var ids = "abc".ToDictionary(c => c.ToString(), c => Counter.Handles[c.ToString()].Id);

        root.SetRoot(new CounterList(["c", "a", "b"], log));
        root.Update(Viewport);

        Assert.AreEqual(10, Counter.Handles["a"].Count.Value);
        Assert.AreEqual(30, Counter.Handles["c"].Count.Value);
        foreach (var (name, id) in ids)
        {
            Assert.AreEqual(id, Counter.Handles[name].Id, $"{name} was remounted");
        }
    }

    [TestMethod]
    public void ReorderedKeyedChildrenAreDrawnInTheNewOrder()
    {
        var log = new Log();
        using var root = Mount(new CounterList(["a", "b", "c"], log));
        var list = root.RootRenderNode.Children.Single();
        var before = list.Children.ToArray();

        root.SetRoot(new CounterList(["c", "a", "b"], log));
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { before[2], before[0], before[1] }, list.Children.ToArray());
    }

    [TestMethod]
    public void UnkeyedChildrenMatchByPosition()
    {
        var log = new Log();
        using var root = Mount(new CounterList(["a", "b"], log, Keyed: false));
        Counter.Handles["a"].Count.Set(5);
        root.Update(Viewport);
        var id = Counter.Handles["a"].Id;

        // Same position, same type: the first counter's state carries over to the new first.
        root.SetRoot(new CounterList(["x", "b"], log, Keyed: false));
        root.Update(Viewport);

        Assert.AreEqual(5, Counter.Handles["x"].Count.Value);
        Assert.AreEqual(id, Counter.Handles["x"].Id);
    }

    [TestMethod]
    public void ANullHoldsItsPlaceSoLaterSiblingsKeepTheirState()
    {
        var log = new Log();
        Element Tree(bool first) => new Box { Children = [first ? new Box() : null, new Counter("b", log)] };
        using var root = Mount(Tree(true));
        Counter.Handles["b"].Count.Set(7);
        root.Update(Viewport);
        var id = Counter.Handles["b"].Id;

        root.SetRoot(Tree(false));
        root.Update(Viewport);

        Assert.AreEqual(id, Counter.Handles["b"].Id);
        Assert.AreEqual(7, Counter.Handles["b"].Count.Value);
    }

    [TestMethod]
    public void ChangingTypeRemountsAndDropsState()
    {
        var log = new Log();
        Element Tree(bool counter) => new Box { Children = [counter ? new Counter("a", log) : new TextBlock("a")] };
        using var root = Mount(Tree(true));
        var id = Counter.Handles["a"].Id;

        root.SetRoot(Tree(false));
        root.Update(Viewport);
        root.SetRoot(Tree(true));
        root.Update(Viewport);

        Assert.AreNotEqual(id, Counter.Handles["a"].Id);
        Assert.AreEqual(0, Counter.Handles["a"].Count.Value);
    }

    [TestMethod]
    public void RemovedChildrenAreUnmountedAndTheirRenderNodesFreed()
    {
        var log = new Log();
        using var root = Mount(new CounterList(["a", "b", "c"], log));

        root.SetRoot(new CounterList(["b"], log));
        root.Update(Viewport);

        Assert.AreEqual(1, root.RootRenderNode.Children.Single().Children.Count);
    }

    [TestMethod]
    public void HooksCalledInADifferentOrderThrow()
    {
        var flag = new[] { true };
        using var root = Mount(new Lambda(ctx =>
        {
            if (flag[0])
            {
                ctx.UseState(0);
            }
            else
            {
                ctx.UseRef(0);
            }
            return null;
        }));
        var state = ((Lambda)root.RootRenderNode.Owner.Children[0].Element);

        flag[0] = false;
        root.RootRenderNode.Owner.Children[0].Root.MarkDirty(root.RootRenderNode.Owner.Children[0]);

        Assert.ThrowsException<InvalidOperationException>(() => root.Update(Viewport));
        Assert.IsNotNull(state);
    }

    [TestMethod]
    public void UseMemoRecomputesOnlyWhenItsDependenciesChange()
    {
        var computed = 0;
        var input = new Signal<int>(1);
        var other = new Signal<int>(0);
        using var root = Mount(new Lambda(ctx =>
        {
            var value = ctx.Watch(input);
            ctx.Watch(other);
            ctx.UseMemo(() => ++computed, value);
            return null;
        }));

        other.Value = 1;
        root.Update(Viewport);
        Assert.AreEqual(1, computed, "a rebuild with the same dependencies reuses the value");

        input.Value = 2;
        root.Update(Viewport);
        Assert.AreEqual(2, computed);
    }
}
