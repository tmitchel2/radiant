using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class GridListTests
{
    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { Width = 620 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 4; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(new Vector2(620, 800));
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static SemanticsNode[] Tiles(UIRoot root) => [.. All(root.GetSemantics()).Where(n => n.Role == SemanticsRole.ListItem)];

    private static void Click(UIRoot root, int tile, KeyModifiers modifiers = KeyModifiers.None)
    {
        var b = Tiles(root)[tile].Bounds;
        root.PointerDown(new Vector2(b.X + 10, b.Y + 10), PointerButton.Left, modifiers);
        root.PointerUp(new Vector2(b.X + 10, b.Y + 10), PointerButton.Left, modifiers);
        Settle(root);
    }

    private static Element Photos(Signal<IReadOnlySet<int>> selection, bool multi = false, List<int>? opened = null) => new Host(ctx =>
        new GridList(10, i => new SurfaceText($"Photo {i}"), ctx.Watch(selection), s => selection.Value = s)
        {
            MultiSelect = multi,
            OnActivate = opened is null ? null : opened.Add,
            MinTileWidth = 140,
        });

    [TestMethod]
    public void ArrowsMoveAcrossAndDownTheColumns()
    {
        var selection = new Signal<IReadOnlySet<int>>(new HashSet<int>());
        using var root = Mount(Photos(selection));
        Click(root, 1);

        root.KeyDown(KeyCode.Down);
        Settle(root);
        var below = selection.Value.Single();
        root.KeyDown(KeyCode.Right);
        Settle(root);

        // 620 wide with 140 px tiles and 12 px gaps: four columns.
        Assert.AreEqual(5, below);
        Assert.AreEqual(6, selection.Value.Single());
        Assert.IsTrue(Tiles(root)[6].IsFocused, "focus follows");
    }

    [TestMethod]
    public void CommandAndShiftSelectSeveral()
    {
        var selection = new Signal<IReadOnlySet<int>>(new HashSet<int>());
        using var root = Mount(Photos(selection, multi: true));

        Click(root, 0);
        Click(root, 2, KeyChord.CommandModifier);
        Click(root, 5, KeyModifiers.Shift);

        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, selection.Value.ToArray(), "Shift reaches from the last tile chosen");
    }

    [TestMethod]
    public void EnterAndADoubleClickActivate()
    {
        var opened = new List<int>();
        using var root = Mount(Photos(new Signal<IReadOnlySet<int>>(new HashSet<int>()), opened: opened));
        Click(root, 3);

        root.KeyDown(KeyCode.Enter);
        var b = Tiles(root)[7].Bounds;
        root.PointerDown(new Vector2(b.X + 10, b.Y + 10));
        root.PointerUp(new Vector2(b.X + 10, b.Y + 10));
        root.PointerDown(new Vector2(b.X + 10, b.Y + 10));
        root.PointerUp(new Vector2(b.X + 10, b.Y + 10));

        CollectionAssert.AreEqual(new[] { 3, 7 }, opened);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
