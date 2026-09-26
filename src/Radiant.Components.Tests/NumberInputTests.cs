using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class NumberInputTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart, RowGap = 20 },
            Children = [new Box { Layout = new LayoutStyle { Width = 400 }, Children = [element] }, new SurfaceButton("Elsewhere")],
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

    private static SemanticsNode Find(UIRoot root, SemanticsRole role, string label) => All(root).Single(n => n.Role == role && n.Label == label);

    private static void Click(UIRoot root, SemanticsNode node) => Click(root, new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2));

    private static void Click(UIRoot root, Vector2 at)
    {
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    private static void Press(UIRoot root, KeyCode key)
    {
        root.KeyDown(key);
        Settle(root);
    }

    private static Element Quantity(Signal<double> value) => new Host(ctx =>
        new NumberField("Quantity", ctx.Watch(value), v => value.Value = v) { Min = 0, Max = 10, Culture = CultureInfo.InvariantCulture });

    [TestMethod]
    public void TheStepperAndKeysChangeTheNumberWithinItsLimits()
    {
        var value = new Signal<double>(9);
        using var root = Mount(Quantity(value));

        Click(root, Find(root, SemanticsRole.Button, "Increase Quantity"));
        var top = value.Value;
        Click(root, Find(root, SemanticsRole.TextField, "Quantity"));
        Press(root, KeyCode.PageDown);
        var paged = value.Value;
        Press(root, KeyCode.Down);

        Assert.AreEqual(10, top);
        Assert.AreEqual(0, paged, "ten steps down, held at the minimum");
        Assert.AreEqual(0, value.Value);
        Assert.IsTrue(Find(root, SemanticsRole.Button, "Decrease Quantity").Semantics.Disabled);
    }

    [TestMethod]
    public void TypedNumbersAreReadWhenTheFieldIsLeft()
    {
        var value = new Signal<double>(3);
        using var root = Mount(Quantity(value));
        Click(root, Find(root, SemanticsRole.TextField, "Quantity"));

        root.KeyDown(KeyCode.A, KeyChord.CommandModifier);
        root.TextInput("7");
        Settle(root);
        Click(root, Find(root, SemanticsRole.Button, "Elsewhere"));
        var typed = value.Value;
        Click(root, Find(root, SemanticsRole.TextField, "Quantity"));
        root.KeyDown(KeyCode.A, KeyChord.CommandModifier);
        root.TextInput("lots");
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        Assert.AreEqual(7, typed);
        Assert.AreEqual(7, value.Value);
        Assert.AreEqual("7", Find(root, SemanticsRole.TextField, "Quantity").Semantics.Value, "text that isn't a number is put back");
    }

    [TestMethod]
    public void DecimalsRoundAndTheCultureReadsTheNumber()
    {
        var value = new Signal<double>(1.5);
        using var root = Mount(new Host(ctx => new NumberField("Scale", ctx.Watch(value), v => value.Value = v)
        {
            Decimals = 2,
            Step = 0.25,
            Culture = CultureInfo.GetCultureInfo("de-DE"),
        }));
        Click(root, Find(root, SemanticsRole.TextField, "Scale"));

        root.KeyDown(KeyCode.A, KeyChord.CommandModifier);
        root.TextInput("2,345");
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        Assert.AreEqual(2.35, value.Value, 1e-9, "a German decimal comma, rounded to two places");
        Assert.AreEqual("2,35", Find(root, SemanticsRole.TextField, "Scale").Semantics.Value);
    }

    private static Element Price(Signal<(float, float)> range) => new Host(ctx =>
    {
        var (low, high) = ctx.Watch(range);
        return new RangeSlider(low, high, (l, h) => range.Value = (l, h)) { Min = 0, Max = 100, Step = 1, Label = "Price" };
    });

    [TestMethod]
    public void APressMovesTheNearerHandle()
    {
        var range = new Signal<(float, float)>((20, 80));
        using var root = Mount(Price(range));
        var track = All(root).Single(n => n.Role == SemanticsRole.Group && n.Label == "Price").Bounds;
        float X(float value) => track.X + 10 + (track.Width - 20) * value / 100;

        Click(root, new Vector2(X(30), track.Y + track.Height / 2));
        var low = range.Value;
        Click(root, new Vector2(X(90), track.Y + track.Height / 2));

        Assert.AreEqual((30f, 80f), low);
        Assert.AreEqual((30f, 90f), range.Value);
    }

    [TestMethod]
    public void TheHandlesCantCross()
    {
        var range = new Signal<(float, float)>((40, 60));
        using var root = Mount(Price(range));
        Click(root, Find(root, SemanticsRole.Slider, "Price minimum"));

        Press(root, KeyCode.End);
        var met = range.Value;
        Press(root, KeyCode.PageUp);

        Assert.AreEqual((60f, 60f), met, "End takes the start up to the end");
        Assert.AreEqual((60f, 60f), range.Value, "and no further");
        Assert.AreEqual("60", Find(root, SemanticsRole.Slider, "Price maximum").Semantics.Value);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
