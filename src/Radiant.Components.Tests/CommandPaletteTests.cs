using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class CommandPaletteTests
{
    private static readonly Vector2 Viewport = new(900, 700);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new LayoutStyle { FlexGrow = 1 }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 30; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static string[] Listed(UIRoot root) => [.. All(root).Where(n => n.Role == SemanticsRole.ListItem).Select(n => n.Label!)];

    private static string? Highlighted(UIRoot root) => All(root).SingleOrDefault(n => n.Role == SemanticsRole.ListItem && n.Semantics.Selected)?.Label;

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    private static void Type(UIRoot root, string text)
    {
        root.TextInput(text);
        Settle(root);
    }

    [TestMethod]
    public void MatchesMustBeInOrder()
    {
        Assert.IsNotNull(FuzzyMatch.Score("tdt", "Toggle dark theme"));
        Assert.IsNull(FuzzyMatch.Score("tdx", "Toggle dark theme"));
        Assert.IsNull(FuzzyMatch.Score("eht", "theme"));
        Assert.AreEqual(0, FuzzyMatch.Score("", "anything"));
    }

    [TestMethod]
    public void WordStartsAndRunsScoreHigher()
    {
        Assert.IsTrue(FuzzyMatch.Score("tdt", "Toggle dark theme") > FuzzyMatch.Score("tdt", "outdated items"), "initials beat letters mid-word");
        Assert.IsTrue(FuzzyMatch.Score("them", "theme") > FuzzyMatch.Score("them", "the manual"), "a run beats scattered letters");
        Assert.IsTrue(FuzzyMatch.Score("op", "Open file") > FuzzyMatch.Score("op", "Stop"), "the start beats the middle");
        Assert.IsTrue(FuzzyMatch.Score("gd", "goToDefinition") > FuzzyMatch.Score("gd", "gradients"), "camel case starts words");
    }

    [TestMethod]
    public void PreferringWordStartsNeverLosesAMatch()
    {
        Assert.IsNotNull(FuzzyMatch.Score("ba", "xba Bx"));
    }

    private static Command[] Commands(List<string> ran) =>
    [
        new("open", "Open file") { Group = "File", Shortcut = "⌘O", Icon = "folder_open", Run = () => ran.Add("open") },
        new("save", "Save") { Group = "File", Shortcut = "⌘S", Icon = "save", Run = () => ran.Add("save") },
        new("dark", "Toggle dark theme") { Group = "View", Keywords = "night appearance", Icon = "palette", Run = () => ran.Add("dark") },
        new("settings", "Open settings") { Group = "Preferences", Icon = "settings", Run = () => ran.Add("settings") },
    ];

    private static Element Palette(Signal<bool> open, List<string> ran) => new Host(ctx =>
        new CommandPalette(ctx.Watch(open), () => open.Value = false, Commands(ran)));

    [TestMethod]
    public void WithNothingTypedEveryCommandShowsByGroup()
    {
        using var root = Mount(Palette(new Signal<bool>(true), []));

        CollectionAssert.AreEqual(new[] { "Open file", "Save", "Toggle dark theme", "Open settings" }, Listed(root));
        Assert.IsTrue(All(root).Any(n => n.Label == "View"), "group headings show");
        Assert.AreEqual("Open file", Highlighted(root));
        Assert.IsTrue(All(root).Single(n => n.Role == SemanticsRole.TextField).IsFocused, "the search has focus");
    }

    [TestMethod]
    public void TypingRanksTheMatches()
    {
        using var root = Mount(Palette(new Signal<bool>(true), []));

        Type(root, "opse");
        var ranked = Listed(root);
        root.TextInput("x");
        Settle(root);

        CollectionAssert.AreEqual(new[] { "Open settings" }, ranked);
        Assert.IsTrue(All(root).Any(n => n.Label == "No matching commands"));
    }

    [TestMethod]
    public void KeywordsFindCommandsToo()
    {
        using var root = Mount(Palette(new Signal<bool>(true), []));

        Type(root, "night");

        CollectionAssert.AreEqual(new[] { "Toggle dark theme" }, Listed(root));
    }

    [TestMethod]
    public void ArrowsAndEnterRunACommandAndClose()
    {
        var open = new Signal<bool>(true);
        var ran = new List<string>();
        using var root = Mount(Palette(open, ran));

        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Up);
        var highlighted = Highlighted(root);
        Press(root, KeyCode.Enter);

        Assert.AreEqual("Save", highlighted);
        CollectionAssert.AreEqual(new[] { "save" }, ran);
        Assert.IsFalse(open.Value);
    }

    [TestMethod]
    public void APressRunsTheCommandUnderIt()
    {
        var ran = new List<string>();
        using var root = Mount(Palette(new Signal<bool>(true), ran));
        var item = All(root).Single(n => n.Label == "Toggle dark theme" && n.Role == SemanticsRole.ListItem).Bounds;

        root.PointerMove(new Vector2(item.X + 20, item.Y + item.Height / 2));
        Settle(root);
        var hovered = Highlighted(root);
        root.PointerDown(new Vector2(item.X + 20, item.Y + item.Height / 2));
        root.PointerUp(new Vector2(item.X + 20, item.Y + item.Height / 2));
        Settle(root);

        Assert.AreEqual("Toggle dark theme", hovered, "the pointer highlights");
        CollectionAssert.AreEqual(new[] { "dark" }, ran);
    }

    [TestMethod]
    public void EscapeClosesAndReopeningStartsAfresh()
    {
        var open = new Signal<bool>(true);
        using var root = Mount(Palette(open, []));
        Type(root, "save");

        Press(root, KeyCode.Escape);
        var closed = !open.Value;
        open.Value = true;
        Settle(root);

        Assert.IsTrue(closed);
        Assert.AreEqual(4, Listed(root).Length, "the search is empty again");
    }

    private sealed record Host(System.Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
