using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class DropZoneTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element, HeadlessPlatform platform)
    {
        var root = new UIRoot(PlatformContext.Platform.Provide(platform, new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20) },
            Children = [element],
        })));
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

    private static readonly FileFilter[] Images = [new("Images", ["png", "jpg"])];

    [TestMethod]
    public void OnlyTheKindsItTakesAreAccepted()
    {
        CollectionAssert.AreEqual(new[] { "/a/cat.PNG", "/a/dog.jpg" }, DropZone.Accepted(["/a/cat.PNG", "/a/notes.txt", "/a/dog.jpg"], Images).ToArray());
        CollectionAssert.AreEqual(new[] { "/a/notes.txt" }, DropZone.Accepted(["/a/notes.txt"], []).ToArray(), "no filters take anything");
    }

    [TestMethod]
    public void DroppedFilesArriveAndRefusedOnesAreCounted()
    {
        var received = new List<IReadOnlyList<string>>();
        using var root = Mount(new DropZone(received.Add) { Filters = Images }, new HeadlessPlatform());
        var zone = All(root).Single(n => n.Role == SemanticsRole.Group && n.Label == "Drop files here").Bounds;

        root.DropFiles(new Vector2(zone.X + 20, zone.Y + 20), ["/a/cat.png", "/a/notes.txt"]);
        Settle(root);

        CollectionAssert.AreEqual(new[] { "/a/cat.png" }, received.Single().ToArray());
        Assert.IsTrue(All(root).Any(n => n.Label == "1 file wasn't a kind this takes"));
    }

    [TestMethod]
    public void DropsElsewhereAreNotTaken()
    {
        var received = new List<IReadOnlyList<string>>();
        using var root = Mount(new DropZone(received.Add) { Layout = new LayoutStyle { Height = 160, AlignSelf = Align.FlexStart, Width = 300 } }, new HeadlessPlatform());

        root.DropFiles(new Vector2(550, 350), ["/a/cat.png"]);

        Assert.AreEqual(0, received.Count);
    }

    [TestMethod]
    public void BrowseOpensThePlatformsPanel()
    {
        var received = new List<IReadOnlyList<string>>();
        var platform = new HeadlessPlatform();
        OpenFileOptions? asked = null;
        platform.Dialogs.OnOpen = options =>
        {
            asked = options;
            return ["/b/photo.jpg"];
        };
        using var root = Mount(new DropZone(received.Add) { Filters = Images }, platform);
        var browse = All(root).Single(n => n.Role == SemanticsRole.Button && n.Label == "Browse").Bounds;

        root.PointerDown(new Vector2(browse.X + 5, browse.Y + 5));
        root.PointerUp(new Vector2(browse.X + 5, browse.Y + 5));
        Settle(root);

        CollectionAssert.AreEqual(new[] { "/b/photo.jpg" }, received.Single().ToArray());
        Assert.IsTrue(asked!.AllowMultiple);
        Assert.AreEqual("Images", asked.Filters.Single().Name);
    }
}
