using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class EffectTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static Element Logging(string name, Log log, Element? child = null, object? deps = null, Signal<int>? trigger = null) =>
        new Lambda(ctx =>
        {
            if (trigger is not null)
            {
                ctx.Watch(trigger);
            }
            ctx.UseEffect(() =>
            {
                log.Add($"effect {name}");
                return () => log.Add($"cleanup {name}");
            }, deps);
            return child;
        }, name);

    [TestMethod]
    public void EffectsRunAfterUpdateNotDuringBuild()
    {
        var log = new Log();
        using var root = new UIRoot(Logging("a", log));

        root.Update(Vector2.Zero);

        CollectionAssert.AreEqual(new[] { "effect a" }, log.Entries);
    }

    [TestMethod]
    public void ChildEffectsRunBeforeTheirParents()
    {
        var log = new Log();
        using var root = new UIRoot(Logging("parent", log, Logging("child", log)));

        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "effect child", "effect parent" }, log.Entries);
    }

    [TestMethod]
    public void AnEffectWithDependenciesRunsAgainOnlyWhenTheyChange()
    {
        var log = new Log();
        var trigger = new Signal<int>(0);
        var deps = new Signal<int>(0);
        using var root = new UIRoot(new Lambda(ctx =>
        {
            ctx.Watch(trigger);
            var d = ctx.Watch(deps);
            ctx.UseEffect(() =>
            {
                log.Add($"effect {d}");
                return () => log.Add($"cleanup {d}");
            }, d);
            return null;
        }));
        root.Update(Viewport);

        trigger.Value = 1;
        root.Update(Viewport);
        deps.Value = 1;
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "effect 0", "cleanup 0", "effect 1" }, log.Entries);
    }

    [TestMethod]
    public void AnEffectWithoutDependenciesRunsAfterEveryBuild()
    {
        var log = new Log();
        var trigger = new Signal<int>(0);
        using var root = new UIRoot(new Lambda(ctx =>
        {
            ctx.Watch(trigger);
            ctx.UseEffect(() =>
            {
                log.Add("effect");
                return null;
            });
            return null;
        }));
        root.Update(Viewport);

        trigger.Value = 1;
        root.Update(Viewport);

        Assert.AreEqual(2, log.Count("effect"));
    }

    [TestMethod]
    public void UnmountingRunsCleanups()
    {
        var log = new Log();
        using var root = new UIRoot(new Box { Children = [Logging("a", log, deps: default(ValueTuple))] });
        root.Update(Viewport);

        root.SetRoot(new Box());
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "effect a", "cleanup a" }, log.Entries);
    }

    [TestMethod]
    public void DisposingTheRootRunsEveryCleanup()
    {
        var log = new Log();
        var root = new UIRoot(Logging("parent", log, Logging("child", log)));
        root.Update(Viewport);
        log.Clear();

        root.Dispose();

        CollectionAssert.AreEquivalent(new[] { "cleanup child", "cleanup parent" }, log.Entries);
    }

    [TestMethod]
    public void StateSetByAnEffectIsBuiltInTheSameUpdate()
    {
        var builds = 0;
        using var root = new UIRoot(new Lambda(ctx =>
        {
            builds++;
            var loaded = ctx.UseState(false);
            ctx.UseEffect(() =>
            {
                loaded.Set(true);
                return null;
            }, default(ValueTuple));
            return loaded.Value ? new TextBlock("loaded") : null;
        }));

        root.Update(Viewport);

        Assert.AreEqual(2, builds);
        Assert.IsFalse(root.NeedsUpdate);
    }

    [TestMethod]
    public void AnEffectThatAlwaysChangesStateIsReportedAsALoop()
    {
        using var root = new UIRoot(new Lambda(ctx =>
        {
            var n = ctx.UseState(0);
            ctx.UseEffect(() =>
            {
                n.Set(n.Value + 1);
                return null;
            });
            return null;
        }));

        Assert.ThrowsException<InvalidOperationException>(() => root.Update(Viewport));
    }
}
