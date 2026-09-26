using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Animation;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class AnimationTests
{
    private static readonly Vector2 Viewport = new(100, 100);

    private static void Frames(UIRoot root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    [TestMethod]
    public void ATransitionMovesToItsTargetOverTimeAndStops()
    {
        var target = new Signal<float>(0f);
        var seen = new List<float>();
        using var root = new UIRoot(new Lambda(ctx =>
        {
            seen.Add(ctx.UseTransition(ctx.Watch(target), TimeSpan.FromMilliseconds(100), Easing.Linear));
            return null;
        }));
        root.Update(Viewport);

        target.Value = 1f;
        root.Update(Viewport);
        Frames(root, 3);
        var partWay = seen[^1];
        Frames(root, 10);

        Assert.AreEqual(0f, seen[0], "starts at the first target without animating");
        Assert.IsTrue(partWay is > 0.3f and < 0.8f, $"{partWay}");
        Assert.AreEqual(1f, seen[^1]);
        Assert.IsFalse(root.NeedsUpdate, "no more frames once it lands");
    }

    [TestMethod]
    public void ANewTargetMidwayStartsFromWhereTheValueIs()
    {
        var target = new Signal<float>(0f);
        var seen = new List<float>();
        using var root = new UIRoot(new Lambda(ctx =>
        {
            seen.Add(ctx.UseTransition(ctx.Watch(target), TimeSpan.FromMilliseconds(100), Easing.Linear));
            return null;
        }));
        root.Update(Viewport);
        target.Value = 1f;
        root.Update(Viewport);
        Frames(root, 3);
        var turn = seen[^1];

        target.Value = 0f;
        root.Update(Viewport);
        Frames(root, 1);

        Assert.IsTrue(seen[^1] < turn && seen[^1] > 0f, $"{seen[^1]} after turning at {turn}");
    }

    [TestMethod]
    public void PresenceKeepsContentUntilItHasLeft()
    {
        var visible = new Signal<bool>(true);
        var progress = new List<float>();
        var app = new Lambda(ctx => new Presence(ctx.Watch(visible), p =>
        {
            progress.Add(p);
            return new Box();
        }) { AnimateOnMount = false });
        using var root = new UIRoot(new Box { Children = [app] });
        root.Update(Viewport);
        Assert.AreEqual(1f, progress[^1]);

        visible.Value = false;
        root.Update(Viewport);
        Frames(root, 3);
        Assert.AreEqual(1, root.RootRenderNode.Children[0].Children.Count, "still there while leaving");

        Frames(root, 20);
        Assert.AreEqual(0, root.RootRenderNode.Children[0].Children.Count, "gone once it has left");
    }

    [TestMethod]
    public void PresenceAnimatesInWhenMounted()
    {
        var progress = new List<float>();
        using var root = new UIRoot(new Presence(true, p =>
        {
            progress.Add(p);
            return null;
        }));
        root.Update(Viewport);
        Frames(root, 20);

        Assert.AreEqual(0f, progress[0]);
        Assert.AreEqual(1f, progress[^1]);
    }
}
