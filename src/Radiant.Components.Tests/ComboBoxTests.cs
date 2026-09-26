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
public class ComboBoxTests
{
    private static readonly Vector2 Viewport = new(600, 600);

    private static readonly string[] Cities = ["London", "Lagos", "Lima", "Oslo", "Paris", "Tokyo"];

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element, new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Elsewhere" }, Layout = new LayoutStyle { Width = 40, Height = 40, Margin = new Edges(0, 400, 0, 0) } }],
        }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        for (var i = 0; i < 10; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);

    private static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    private static SemanticsNode Field(UIRoot root) => All(root).Single(n => n.Role == SemanticsRole.TextField);

    private static string[] Listed(UIRoot root) => [.. All(root).Where(n => n.Role == SemanticsRole.ListItem).Select(n => n.Label!)];

    private static string? Highlighted(UIRoot root) => All(root).SingleOrDefault(n => n.Role == SemanticsRole.ListItem && n.Semantics.Selected)?.Label;

    private static Vector2 Centre(SemanticsNode node) => new(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);

    private static void Click(UIRoot root, SemanticsNode node)
    {
        root.PointerDown(Centre(node));
        root.PointerUp(Centre(node));
        Settle(root);
    }

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

    private static Element Combo(Signal<string?> value, bool allowCustom = false) => new Host(ctx =>
        new ComboBox("City", Cities, ctx.Watch(value), v => value.Value = v) { AllowCustom = allowCustom });

    [TestMethod]
    public void TypingSuggestsTheBestMatchesFirst()
    {
        using var root = Mount(Combo(new Signal<string?>(null)));
        Click(root, Field(root));

        Type(root, "lo");

        CollectionAssert.AreEqual(new[] { "London", "Lagos", "Oslo" }, Listed(root), "word starts and runs first");
        Assert.IsTrue(Field(root).IsFocused, "focus stays in the field");
    }

    [TestMethod]
    public void ArrowsAndEnterChoose()
    {
        var value = new Signal<string?>(null);
        using var root = Mount(Combo(value));
        Click(root, Field(root));

        Press(root, KeyCode.Down);
        var opened = Listed(root).Length;
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Down);
        Press(root, KeyCode.Up);
        Press(root, KeyCode.Enter);

        Assert.AreEqual(Cities.Length, opened, "Down opens the whole list");
        Assert.AreEqual("Lagos", value.Value);
        Assert.AreEqual("Lagos", Field(root).Semantics.Value);
        Assert.AreEqual(0, Listed(root).Length, "choosing closes the list");
    }

    [TestMethod]
    public void APressOnASuggestionChoosesItAndKeepsFocus()
    {
        var value = new Signal<string?>(null);
        using var root = Mount(Combo(value));
        Click(root, Field(root));
        Type(root, "to");

        Click(root, All(root).Single(n => n.Role == SemanticsRole.ListItem && n.Label == "Tokyo"));

        Assert.AreEqual("Tokyo", value.Value);
        Assert.IsTrue(Field(root).IsFocused);
    }

    [TestMethod]
    public void EscapePutsTheTextBack()
    {
        var value = new Signal<string?>("Paris");
        using var root = Mount(Combo(value));
        Click(root, Field(root));

        Type(root, "xyz");
        Press(root, KeyCode.Escape);

        Assert.AreEqual("Paris", Field(root).Semantics.Value);
        Assert.AreEqual("Paris", value.Value);
    }

    [TestMethod]
    public void LeavingWithUnknownTextRevertsUnlessCustomIsAllowed()
    {
        var strict = new Signal<string?>("Oslo");
        using (var root = Mount(Combo(strict)))
        {
            Click(root, Field(root));
            Type(root, "Atlantis");
            Click(root, All(root).Single(n => n.Label == "Elsewhere"));

            Assert.AreEqual("Oslo", strict.Value);
            Assert.AreEqual("Oslo", Field(root).Semantics.Value);
        }

        var custom = new Signal<string?>("Oslo");
        using (var root = Mount(Combo(custom, allowCustom: true)))
        {
            Click(root, Field(root));
            Type(root, "Atlantis");
            Click(root, All(root).Single(n => n.Label == "Elsewhere"));

            Assert.AreEqual("OsloAtlantis", custom.Value, "the typed text, appended to the value, is kept");
        }
    }

    [TestMethod]
    public void TypingAnOptionExactlyChoosesIt()
    {
        var value = new Signal<string?>(null);
        using var root = Mount(Combo(value));
        Click(root, Field(root));

        Type(root, "lima");
        Press(root, KeyCode.Escape);
        Type(root, "lima");
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        Assert.AreEqual("Lima", value.Value, "Enter picks the highlighted best match");
    }

    [TestMethod]
    public void AValueSetFromOutsideShowsInTheField()
    {
        var value = new Signal<string?>("Lima");
        using var root = Mount(Combo(value));

        value.Value = "Tokyo";
        Settle(root);

        Assert.AreEqual("Tokyo", Field(root).Semantics.Value);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
