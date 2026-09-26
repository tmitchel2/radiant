using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.ColorSystem;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class ColorPickerTests
{
    private static readonly Vector2 Viewport = new(500, 500);

    private const int Purple = unchecked((int)0xFF6750A4);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20), AlignItems = Align.FlexStart },
            Children = [element, new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Elsewhere" }, Layout = new LayoutStyle { Width = 40, Height = 40 } }],
        }));
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

    private static SemanticsNode Slider(UIRoot root, string label) => All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.Slider && n.Label == label);

    private static Element Picker(Signal<int> value) => new Host(ctx => new ColorPicker(ctx.Watch(value), v => value.Value = v));

    [TestMethod]
    public void HexReadsSixOrThreeDigitsWithOrWithoutAHash()
    {
        Assert.IsTrue(ColorPicker.TryParseHex("#6750A4", out var six));
        Assert.IsTrue(ColorPicker.TryParseHex("f0a", out var three));
        Assert.IsFalse(ColorPicker.TryParseHex("#12345", out _));
        Assert.IsFalse(ColorPicker.TryParseHex("banana", out _));

        Assert.AreEqual(Purple, six);
        Assert.AreEqual(unchecked((int)0xFFFF00AA), three);
        Assert.AreEqual("#6750A4", ColorPicker.Hex(Purple));
    }

    [TestMethod]
    public void ThePlaneIsClearWhereTheScreenCantShowTheColour()
    {
        var plane = ColorPicker.Plane(50);

        // Full chroma at tone 50 is out of gamut for most hues; the lowest chroma never is.
        var pixels = plane.Pixels.Span;
        Assert.AreEqual(0, pixels[3], "top left: hue 1, chroma 119");
        Assert.AreEqual(255, pixels[(59 * 180 + 90) * 4 + 3], "bottom middle: chroma 1");
    }

    [TestMethod]
    public void PressingThePlaneSetsHueAndChroma()
    {
        var value = new Signal<int>(Purple);
        using var root = Mount(Picker(value));
        var plane = Slider(root, "Hue and chroma").Bounds;

        // A quarter across is hue 90; three quarters down is chroma 30, at the colour's tone.
        var at = new Vector2(plane.X + plane.Width * 0.25f, plane.Y + plane.Height * 0.75f);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);

        var picked = Hct.FromInt(value.Value);
        Assert.AreEqual(90, picked.Hue, 3);
        Assert.AreEqual(30, picked.Chroma, 3);
        Assert.AreEqual(Hct.FromInt(Purple).Tone, picked.Tone, 1);
    }

    [TestMethod]
    public void DraggingTheStripChangesOnlyTheTone()
    {
        var value = new Signal<int>(Purple);
        using var root = Mount(Picker(value));
        var strip = Slider(root, "Tone").Bounds;
        var before = Hct.FromInt(Purple);

        root.PointerDown(new Vector2(strip.X + 10, strip.Y + 10));
        root.PointerMove(new Vector2(strip.X + strip.Width * 0.8f, strip.Y + 200));
        root.PointerUp(new Vector2(strip.X + strip.Width * 0.8f, strip.Y + 200));
        Settle(root);

        var after = Hct.FromInt(value.Value);
        Assert.AreEqual(80, after.Tone, 1, "followed the pointer off the strip");
        Assert.AreEqual(before.Hue, after.Hue, 2);
    }

    [TestMethod]
    public void TheKeyboardStepsEachPart()
    {
        var value = new Signal<int>(Purple);
        using var root = Mount(Picker(value));
        var plane = Slider(root, "Hue and chroma").Bounds;
        root.PointerDown(new Vector2(plane.X + 144, plane.Y + 80));
        root.PointerUp(new Vector2(plane.X + 144, plane.Y + 80));
        Settle(root);
        var start = Hct.FromInt(value.Value);

        root.KeyDown(KeyCode.Right, KeyModifiers.Shift);
        root.KeyDown(KeyCode.Down);
        Settle(root);
        var moved = Hct.FromInt(value.Value);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.End);
        Settle(root);

        Assert.AreEqual(start.Hue + 10, moved.Hue, 2, "Shift steps ten");
        Assert.AreEqual(start.Chroma - 1, moved.Chroma, 1.5);
        Assert.AreEqual(100, Hct.FromInt(value.Value).Tone, 0.5, "End on the strip is white");
    }

    [TestMethod]
    public void TypedHexIsTakenAndBadHexPutBack()
    {
        var value = new Signal<int>(Purple);
        using var root = Mount(Picker(value));
        var field = All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.TextField);
        var at = new Vector2(field.Bounds.X + 20, field.Bounds.Y + field.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);

        root.KeyDown(KeyCode.A, KeyChord.CommandModifier);
        root.TextInput("#00897B");
        root.KeyDown(KeyCode.Enter);
        Settle(root);
        var taken = value.Value;
        root.KeyDown(KeyCode.A, KeyChord.CommandModifier);
        root.TextInput("nope");
        root.KeyDown(KeyCode.Enter);
        Settle(root);

        Assert.AreEqual(unchecked((int)0xFF00897B), taken);
        Assert.AreEqual("#00897B", All(root.GetSemantics()).Single(n => n.Role == SemanticsRole.TextField).Semantics.Value);
    }

    private sealed record Host(Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }
}
