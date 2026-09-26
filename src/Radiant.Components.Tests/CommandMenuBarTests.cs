using System;
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
public class CommandMenuBarTests
{
    private static readonly Vector2 Viewport = new(800, 500);

    private static UIRoot Mount(Element element, HeadlessPlatform platform)
    {
        var root = new UIRoot(PlatformContext.Platform.Provide(platform, new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, AlignItems = Align.FlexStart },
            Children = [element],
        })));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 20; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    // An app's commands: File and View menus, a command with no menu, and a setting.
    private sealed record App(List<string> Ran, Signal<bool> Dark, Signal<bool> CanSave) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var (ran, dark) = (Ran, Dark);
            context.UseCommand(new Command("zoom", "Zoom in") { Menu = "View", Shortcut = KeyChord.Command(KeyCode.Equal), Run = () => ran.Add("zoom") });
            context.UseCommand(new Command("dark", "Dark theme") { Menu = "View", Group = "Theme", Checked = context.Watch(dark), Run = () => dark.Value = !dark.Value });
            context.UseCommand(new Command("new", "New") { Menu = "File", Shortcut = KeyChord.Command(KeyCode.N), Run = () => ran.Add("new") });
            context.UseCommand(new Command("save", "Save") { Menu = "File", Shortcut = KeyChord.Command(KeyCode.S), Enabled = context.Watch(CanSave), Run = () => ran.Add("save") });
            context.UseCommand(new Command("delete", "Delete") { Menu = "File", Group = "Danger", Shortcut = new KeyChord(KeyCode.Delete), Run = () => ran.Add("delete") });
            context.UseCommand(new Command("palette", "Command palette") { Shortcut = KeyChord.Command(KeyCode.K) });
            return new CommandMenuBar();
        }
    }

    [TestMethod]
    public void OnAPlatformMenuBarTheCommandsAreItsMenus()
    {
        var (ran, dark, canSave) = (new List<string>(), new Signal<bool>(false), new Signal<bool>(false));
        var platform = new HeadlessPlatform { Menus = { IsSupported = true, HasMenuBar = true } };
        using var root = Mount(new App(ran, dark, canSave), platform);

        var bar = platform.Menus.MenuBar;
        CollectionAssert.AreEqual(new[] { "File", "View" }, bar.Select(m => m.Title).ToArray(), "File before View, and nothing for commands without a menu");
        var file = bar[0].Items;
        CollectionAssert.AreEqual(new[] { "New", "Save", "", "Delete" }, file.Select(i => i.Title).ToArray());
        Assert.IsTrue(file[2].IsSeparator, "a divider between groups");
        Assert.IsFalse(file[1].Enabled);
        Assert.AreEqual(new MenuShortcut("n", MenuModifiers.Command), file[0].Shortcut);
        Assert.IsNull(file[3].Shortcut, "a bare key isn't given to the platform's menu");
        Assert.IsFalse(bar[1].Items[^1].Checked);

        platform.Menus.ChooseFromMenuBar(0, 0);
        platform.Menus.ChooseFromMenuBar(0, 1);
        platform.Menus.ChooseFromMenuBar(1, 2);
        Settle(root);

        CollectionAssert.AreEqual(new[] { "new" }, ran, "a disabled item does nothing");
        Assert.IsTrue(dark.Value);
        Assert.IsTrue(platform.Menus.MenuBar[1].Items[^1].Checked, "the menu bar follows the commands");
        Assert.IsFalse(All(root.GetSemantics()).Any(n => n.Label == "Menu bar"), "nothing is drawn");
    }

    [TestMethod]
    public void WithoutOneTheMenuBarIsDrawnAndRunsTheCommands()
    {
        var (ran, dark, canSave) = (new List<string>(), new Signal<bool>(false), new Signal<bool>(true));
        using var root = Mount(new App(ran, dark, canSave), new HeadlessPlatform());

        var titles = All(root.GetSemantics()).Single(n => n.Label == "Menu bar").Children.Select(n => n.Label).ToArray();
        CollectionAssert.AreEqual(new[] { "File", "View" }, titles);

        var file = All(root.GetSemantics()).First(n => n.Label == "File");
        root.PointerDown(Centre(file));
        root.PointerUp(Centre(file));
        Settle(root);
        var save = All(root.GetSemantics()).First(n => n.Role == SemanticsRole.MenuItem && n.Label!.StartsWith("Save", StringComparison.Ordinal));
        root.PointerDown(Centre(save));
        root.PointerUp(Centre(save));
        Settle(root);

        CollectionAssert.AreEqual(new[] { "save" }, ran);
    }

    [TestMethod]
    public void ShortcutsRunTheCommands()
    {
        var (ran, dark, canSave) = (new List<string>(), new Signal<bool>(false), new Signal<bool>(true));
        using var root = Mount(new App(ran, dark, canSave), new HeadlessPlatform());

        var save = KeyChord.Command(KeyCode.S);
        root.KeyDown(save.Key, save.Modifiers);
        root.KeyDown(KeyCode.Delete);

        CollectionAssert.AreEqual(new[] { "save", "delete" }, ran);
    }

    [TestMethod]
    public void APaletteListsTheRegisteredCommandsThatCanRun()
    {
        var (ran, dark, canSave) = (new List<string>(), new Signal<bool>(false), new Signal<bool>(false));
        using var root = Mount(new Box
        {
            Children =
            [
                new App(ran, dark, canSave),
                new Host(context => new CommandPalette(true, () => { }, context.UseCommands())),
            ],
        }, new HeadlessPlatform());

        var listed = All(root.GetSemantics()).Where(n => n.Role == SemanticsRole.ListItem).Select(n => n.Label).ToArray();

        CollectionAssert.AreEquivalent(new[] { "Zoom in", "Dark theme", "New", "Delete", "Command palette" }, listed);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
