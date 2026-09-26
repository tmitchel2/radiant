using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class ErrorBoundaryTests
{
    private static readonly Vector2 Viewport = new(200, 100);

    private static string[] Texts(UIRoot root) =>
        [.. Descend(root.GetSemantics()).Where(n => n.Role == SemanticsRole.Text).Select(n => n.Label!)];

    private static System.Collections.Generic.IEnumerable<SemanticsNode> Descend(SemanticsNode node) => node.Children.SelectMany(Descend).Prepend(node);

    private static Element Fallback(Exception error, Action retry) => new TextBlock($"Failed: {error.Message}");

    [TestMethod]
    public void AFailureShowsTheFallbackAndTheRestCarriesOn()
    {
        Exception? reported = null;
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new TextBlock("Before"),
                new ErrorBoundary(new Lambda(_ => throw new InvalidOperationException("broken")), Fallback) { OnError = e => reported = e },
                new TextBlock("After"),
            ],
        });

        root.Update(Viewport);

        CollectionAssert.AreEqual(new[] { "Before", "Failed: broken", "After" }, Texts(root));
        Assert.AreEqual("broken", reported?.Message);
    }

    [TestMethod]
    public void AFailureOnALaterBuildIsCaughtAndRetryBuildsAgain()
    {
        var broken = new Signal<bool>(false);
        Action? retry = null;
        using var root = new UIRoot(new ErrorBoundary(
            new Box { Children = [new Lambda(context => context.Watch(broken) ? throw new InvalidOperationException("now broken") : new TextBlock("Fine"))] },
            (error, again) =>
            {
                retry = again;
                return new TextBlock($"Failed: {error.Message}");
            }));
        root.Update(Viewport);
        CollectionAssert.AreEqual(new[] { "Fine" }, Texts(root));

        broken.Value = true;
        root.Update(Viewport);
        CollectionAssert.AreEqual(new[] { "Failed: now broken" }, Texts(root));

        broken.Value = false;
        retry!();
        root.Update(Viewport);
        CollectionAssert.AreEqual(new[] { "Fine" }, Texts(root));
    }

    [TestMethod]
    public void WithoutABoundaryAFailureStillThrows()
    {
        using var root = new UIRoot(new Lambda(_ => throw new InvalidOperationException("unguarded")));

        Assert.ThrowsException<InvalidOperationException>(() => root.Update(Viewport));
    }
}
