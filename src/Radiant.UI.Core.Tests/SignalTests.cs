using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class SignalTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    [TestMethod]
    public void AWatchingComponentIsRebuiltWhenTheSignalChanges()
    {
        var signal = new Signal<int>(1);
        var log = new Log();
        using var root = new UIRoot(new Lambda(ctx =>
        {
            log.Add($"{ctx.Watch(signal)}");
            return null;
        }));
        root.Update(Viewport);

        signal.Value = 2;
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "1", "2" }, log.Entries);
    }

    [TestMethod]
    public void SettingTheSameValueNotifiesNoOne()
    {
        var signal = new Signal<int>(1);
        var notified = 0;
        using var subscription = signal.Subscribe(() => notified++);

        signal.Value = 1;

        Assert.AreEqual(0, notified);
    }

    [TestMethod]
    public void AnUnmountedComponentStopsWatching()
    {
        var signal = new Signal<int>(1);
        var log = new Log();
        var watcher = new Lambda(ctx =>
        {
            log.Add($"{ctx.Watch(signal)}");
            return null;
        });
        using var root = new UIRoot(new Box { Children = [watcher] });
        root.Update(Viewport);
        root.SetRoot(new Box());
        root.Update(Viewport);

        signal.Value = 2;

        Assert.IsFalse(root.NeedsUpdate);
        CollectionAssert.AreEqual(new[] { "1" }, log.Entries);
    }

    [TestMethod]
    public void ChangesRequestAFrame()
    {
        var signal = new Signal<int>(1);
        var requested = 0;
        using var root = new UIRoot(new Lambda(ctx =>
        {
            ctx.Watch(signal);
            return null;
        }));
        root.Update(Viewport);
        root.FrameRequested = () => requested++;

        signal.Value = 2;

        Assert.AreEqual(1, requested);
    }
}
