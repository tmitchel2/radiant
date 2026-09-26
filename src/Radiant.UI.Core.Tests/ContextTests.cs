using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ContextTests
{
    private static readonly Vector2 Viewport = new(400, 300);
    private static readonly Context<string> Theme = new("light");

    private static Element Reader(Log log) => new Lambda(ctx =>
    {
        log.Add(ctx.Use(Theme));
        return null;
    }, "reader");

    [TestMethod]
    public void WithoutAProviderTheDefaultIsRead()
    {
        var log = new Log();
        using var root = new UIRoot(Reader(log));

        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "light" }, log.Entries);
    }

    [TestMethod]
    public void TheNearestProviderWins()
    {
        var log = new Log();
        using var root = new UIRoot(Theme.Provide("dark", new Box { Children = [Theme.Provide("contrast", Reader(log))] }));

        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "contrast" }, log.Entries);
    }

    [TestMethod]
    public void AChangedValueRebuildsReadersEvenPastComponentsThatDoNotRebuild()
    {
        var log = new Log();
        // The middle component's props never change, so it is never rebuilt; the reader still must be.
        var middle = new Lambda(_ => Reader(log), "middle");
        using var root = new UIRoot(Theme.Provide("dark", middle));
        root.Update(Viewport);

        root.SetRoot(Theme.Provide("light", middle));
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "dark", "light" }, log.Entries);
    }

    [TestMethod]
    public void AnUnchangedValueRebuildsNothing()
    {
        var log = new Log();
        var middle = new Lambda(_ => Reader(log), "middle");
        using var root = new UIRoot(Theme.Provide("dark", middle));
        root.Update(Viewport);

        root.SetRoot(Theme.Provide("dark", middle));
        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "dark" }, log.Entries);
    }
}
