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
public class StackNavigatorTests
{
    private static readonly Vector2 Viewport = new(600, 500);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 40; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static bool Shows(UIRoot root, string text) => All(root).Any(n => n.Label == text);

    private static void Click(UIRoot root, string label)
    {
        var node = All(root).First(n => n.Role is SemanticsRole.Button or SemanticsRole.ListItem && n.Label == label);
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    // A page listing its children; pressing one pushes it.
    private sealed record Folder(string Name, int Depth) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var navigator = context.UseNavigator()!;
            var (name, depth) = (Name, Depth);
            return new Box
            {
                Children =
                [
                    new SurfaceText($"Inside {name}"),
                    new ListItem($"{name}/child") { OnPress = () => navigator.Push($"{name}/child", new Folder($"{name}/child", depth + 1)) },
                ],
            };
        }
    }

    [TestMethod]
    public void PagesPushAndTheBackButtonReturns()
    {
        using var root = Mount(new StackNavigator("Home", new Folder("Home", 0)));
        var atRoot = All(root).Any(n => n.Label == "Back");

        Click(root, "Home/child");
        var pushed = Shows(root, "Inside Home/child") && Shows(root, "Home/child");
        Click(root, "Back");

        Assert.IsFalse(atRoot, "no back button at the root");
        Assert.IsTrue(pushed);
        Assert.IsTrue(Shows(root, "Inside Home"));
        Assert.IsFalse(Shows(root, "Inside Home/child"));
    }

    [TestMethod]
    public void TheBackShortcutPopsAndStopsAtTheRoot()
    {
        using var root = Mount(new StackNavigator("Home", new Folder("Home", 0)));
        Click(root, "Home/child");
        Click(root, "Home/child/child");
        var chord = OperatingSystem.IsMacOS() ? KeyChord.Command(KeyCode.LeftBracket) : new KeyChord(KeyCode.Left, KeyModifiers.Alt);

        root.KeyDown(chord.Key, chord.Modifiers);
        Settle(root);
        var one = Shows(root, "Inside Home/child");
        root.KeyDown(chord.Key, chord.Modifiers);
        root.KeyDown(chord.Key, chord.Modifiers);
        Settle(root);

        Assert.IsTrue(one);
        Assert.IsTrue(Shows(root, "Inside Home"), "popped to the root and no further");
    }

    [TestMethod]
    public void OutsideANavigatorThereIsNone()
    {
        Navigator? found = new Navigator(() => [], _ => { });
        using var root = Mount(new Probe(n => found = n));

        Assert.IsNull(found);
    }

    private sealed record Probe(Action<Navigator?> Seen) : Component
    {
        public override Element? Build(BuildContext context)
        {
            Seen(context.UseNavigator());
            return null;
        }
    }
}
