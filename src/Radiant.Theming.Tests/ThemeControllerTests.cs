using System.Numerics;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Animation;
using Radiant.UI.Core;

namespace Radiant.Theming.Tests;

[TestClass]
public class ThemeControllerTests
{
    private static readonly Theme Dark = new() { Colors = new ThemeColors { IsDark = true } };

    [TestMethod]
    public void EasingsRunFromZeroToOneWithoutGoingBack()
    {
        foreach (var easing in new[] { Easing.Linear, Easing.Standard, Easing.EmphasizedDecelerate, Easing.EmphasizedAccelerate })
        {
            Assert.AreEqual(0f, easing.Evaluate(0f));
            Assert.AreEqual(1f, easing.Evaluate(1f));
            var last = 0f;
            for (var t = 0.05f; t < 1f; t += 0.05f)
            {
                var value = easing.Evaluate(t);
                Assert.IsTrue(value >= last - 1e-4f, $"{easing} at {t}");
                last = value;
            }
        }
        Assert.AreEqual(0.5f, Easing.Linear.Evaluate(0.5f), 1e-4f);
    }

    [TestMethod]
    public void ASetWithoutATransitionIsImmediate()
    {
        var controller = new ThemeController();

        controller.Set(Dark);

        Assert.IsFalse(controller.IsAnimating);
        Assert.IsTrue(controller.Current.Value.Theme.Colors.IsDark);
    }

    [TestMethod]
    public void ATransitionMovesOnWithTimeAndEnds()
    {
        var controller = new ThemeController();
        var start = controller.Current.Value;

        controller.Set(Dark, TimeSpan.FromMilliseconds(300));
        controller.Advance(0.15);

        Assert.IsTrue(controller.IsAnimating);
        Assert.AreNotSame(start, controller.Current.Value);
        controller.Advance(0.2);
        Assert.IsFalse(controller.IsAnimating);
        Assert.IsTrue(controller.Current.Value.Theme.Colors.IsDark);
    }

    [TestMethod]
    public void ReducedMotionSkipsTheTransition()
    {
        var controller = new ThemeController();

        controller.Set(Dark with { Motion = new MotionScheme { Reduced = true } }, TimeSpan.FromSeconds(1));

        Assert.IsFalse(controller.IsAnimating);
    }

    [TestMethod]
    public void ComponentsBelowAProviderFollowTheThemeThroughATransition()
    {
        var controller = new ThemeController();
        var seen = new System.Collections.Generic.List<bool>();
        var reader = new Reader(seen);
        using var root = new UIRoot(new ThemeProvider(controller, reader));
        root.Update(new Vector2(100, 100));

        controller.Set(Dark, TimeSpan.FromMilliseconds(100));
        for (var i = 0; i < 10; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(new Vector2(100, 100));
        }

        Assert.IsTrue(seen.Count > 3, $"{seen.Count} builds");
        Assert.IsFalse(seen[0]);
        Assert.IsTrue(seen[^1]);
    }

    [TestMethod]
    public void AProviderAsksForFramesOnlyWhileATransitionRuns()
    {
        var controller = new ThemeController();
        using var root = new UIRoot(new ThemeProvider(controller, new Reader([])));
        root.Update(new Vector2(100, 100));
        var idle = root.NeedsUpdate;

        controller.Set(Dark, TimeSpan.FromMilliseconds(100));
        var animating = root.NeedsUpdate;
        for (var i = 0; i < 10; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(new Vector2(100, 100));
        }

        Assert.IsFalse(idle, "an idle app draws nothing");
        Assert.IsTrue(animating);
        Assert.IsFalse(root.NeedsUpdate, "done once the transition ends");
    }

    private sealed record Reader(System.Collections.Generic.List<bool> Seen) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            Seen.Add(theme.Theme.Colors.IsDark);
            return null;
        }
    }
}
