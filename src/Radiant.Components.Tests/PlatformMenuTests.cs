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
public class PlatformMenuTests
{
    private static UIRoot Mount(Element element, HeadlessPlatform platform)
    {
        var root = new UIRoot(PlatformContext.Platform.Provide(platform, new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.FlexStart },
            Children = [element],
        })));
        root.Update(new Vector2(600, 400));
        return root;
    }

    private static ContextMenu Menu(List<string> chosen) => new(new Box { Layout = new LayoutStyle { Width = 300, Height = 200 } },
    [
        new MenuItem("Cut", () => chosen.Add("cut")),
        new MenuItem("Copy", () => chosen.Add("copy")) { Disabled = true },
        new MenuItem("Delete", () => chosen.Add("delete")) { DividerBefore = true },
    ]);

    [TestMethod]
    public void TheMenuIsThePlatformsWhereItHasOne()
    {
        var chosen = new List<string>();
        var platform = new HeadlessPlatform { Menus = { IsSupported = true } };
        Vector2? at = null;
        platform.Menus.OnShow = (items, position) =>
        {
            at = position;
            return 3;
        };
        using var root = Mount(Menu(chosen), platform);

        root.PointerDown(new Vector2(120, 80), PointerButton.Right);

        var shown = platform.Menus.LastShown!;
        CollectionAssert.AreEqual(new[] { "Cut", "Copy", "", "Delete" }, shown.Select(i => i.Title).ToArray());
        Assert.IsTrue(shown[2].IsSeparator && !shown[1].Enabled);
        Assert.AreEqual(new Vector2(120, 80), at);
        CollectionAssert.AreEqual(new[] { "delete" }, chosen, "the fourth entry, after the separator, is Delete");
        Assert.IsFalse(Enumerate(root.GetSemantics()).Any(n => n.Role == SemanticsRole.Menu), "no drawn menu as well");
    }

    [TestMethod]
    public void DismissingThePlatformsMenuChoosesNothing()
    {
        var chosen = new List<string>();
        var platform = new HeadlessPlatform { Menus = { IsSupported = true } };
        using var root = Mount(Menu(chosen), platform);

        root.PointerDown(new Vector2(120, 80), PointerButton.Right);

        Assert.AreEqual(0, chosen.Count);
    }

    private static IEnumerable<SemanticsNode> Enumerate(SemanticsNode node) => node.Children.SelectMany(Enumerate).Prepend(node);
}
