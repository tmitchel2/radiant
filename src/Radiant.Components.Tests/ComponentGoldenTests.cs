using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Testing;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

/// <summary>
/// What the core components look like, light and dark, as goldens: each variant and state side by
/// side on a sheet. A change to how any of them draws shows up here as a diff to look at.
/// </summary>
[TestClass]
[TestCategory("Gpu")]
public class ComponentGoldenTests
{
    private static readonly bool[] Modes = [false, true];

    private static GpuCanvas Canvas(int width, int height)
    {
        if (GpuCanvas.TryCreate(width, height, pixelScale: 2f) is { } canvas)
        {
            return canvas;
        }
        Assert.Inconclusive("No GPU available.");
        return null!;
    }

    // The content on the theme's surface, in light or dark.
    private static Element Themed(bool dark, params Element?[] rows) => new ThemeProvider(
        new ThemeController(new Theme { Colors = new Theme().Colors with { IsDark = dark } }),
        new Surface
        {
            SurfaceColor = SurfaceName.Surface,
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(16), RowGap = 12, AlignItems = Align.FlexStart },
            Children = rows,
        });

    private static Box Row(params Element?[] items) => new()
    {
        Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 12 },
        Children = items,
    };

    private static void Check(string name, int width, int height, Func<bool, Element> sheet)
    {
        foreach (var dark in Modes)
        {
            using var canvas = Canvas(width, height);
            Golden.AssertMatches(UISnapshot.Render(canvas, Themed(dark, sheet(dark))), $"{name}_{(dark ? "dark" : "light")}");
        }
    }

    [TestMethod]
    public void Buttons() => Check("Buttons", 480, 300, _ => new Box
    {
        Layout = new LayoutStyle { RowGap = 12 },
        Children =
        [
            .. Enum.GetValues<ButtonVariant>().Select(variant => (Element?)Row(
                new SurfaceButton(variant.ToString(), variant),
                new SurfaceButton("With icon", variant) { Icon = "add" },
                new SurfaceButton("Disabled", variant) { ShowDisabled = true })),
        ],
    });

    [TestMethod]
    public void ButtonStates()
    {
        // Each state is its own render: resting, hovered, focused from the keyboard, and pressed.
        (string Name, Action<UIRoot>? Act)[] states =
        [
            ("rest", null),
            ("hover", root => root.PointerMove(Centre(root))),
            ("focus", root => root.MoveFocus(forward: true)),
            ("pressed", root => root.PointerDown(Centre(root))),
        ];
        foreach (var dark in Modes)
        {
            var columns = new List<Snapshot>();
            foreach (var variant in new[] { ButtonVariant.Filled, ButtonVariant.Outlined })
            {
                var shots = new List<Snapshot>();
                foreach (var (_, act) in states)
                {
                    using var canvas = Canvas(140, 64);
                    shots.Add(UISnapshot.Render(canvas, Themed(dark, new SurfaceButton("Button", variant)), act: act));
                }
                columns.Add(Snapshot.Row(shots));
            }
            Golden.AssertMatches(Snapshot.Column(columns), $"ButtonStates_{(dark ? "dark" : "light")}");
        }

        static Vector2 Centre(UIRoot root)
        {
            var button = All(root.GetSemantics()).First(n => n.Role == SemanticsRole.Button);
            return new Vector2(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2);
        }
    }

    [TestMethod]
    public void SelectionControls() => Check("SelectionControls", 560, 200, _ => new Box
    {
        Layout = new LayoutStyle { RowGap = 12 },
        Children =
        [
            Row(
                new Checkbox(false, _ => { }) { Label = "Off" },
                new Checkbox(true, _ => { }) { Label = "On" },
                new Checkbox(false, _ => { }) { Label = "Some", Indeterminate = true },
                new Checkbox(true, _ => { }) { Label = "Error", Error = true },
                new Checkbox(true, null) { Label = "Disabled", Disabled = true }),
            Row(
                new Radio(false, () => { }) { Label = "Off" },
                new Radio(true, () => { }) { Label = "On" },
                new Radio(true, null) { Label = "Disabled", Disabled = true }),
            Row(
                new Switch(false, _ => { }) { Label = "Off" },
                new Switch(true, _ => { }) { Label = "On" },
                new Switch(true, null) { Label = "Disabled", Disabled = true }),
        ],
    });

    [TestMethod]
    public void Fields() => Check("Fields", 560, 300, _ => new Box
    {
        Layout = new LayoutStyle { RowGap = 16 },
        Children =
        [
            Row(
                new TextField("Name") { Layout = new LayoutStyle { Width = 250 } },
                new TextField("Email") { InitialText = "tom@example.com", LeadingIcon = "mail", Layout = new LayoutStyle { Width = 250 } }),
            Row(
                new TextField("Password") { InitialText = "short", Error = "At least 8 characters", Variant = TextFieldVariant.Outlined, Layout = new LayoutStyle { Width = 250 } },
                new TextField("Disabled") { InitialText = "Can't change", Disabled = true, Layout = new LayoutStyle { Width = 250 } }),
            new Box { Layout = new LayoutStyle { Width = 400 }, Children = [new Slider(0.4f, _ => { }) { Label = "Volume" }] },
        ],
    });

    [TestMethod]
    public void Display() => Check("Display", 560, 360, _ => new Box
    {
        Layout = new LayoutStyle { RowGap = 16 },
        Children =
        [
            Row(
                new Chip("Assist") { Icon = "event" },
                new Chip("Filter") { Selected = true },
                new Chip("Input") { OnRemove = () => { } },
                new Badge(new SurfaceIcon("mail")) { Count = 3 },
                new Badge(new SurfaceIcon("notifications")) { Count = 120 }),
            new Card(new SurfaceText("A card on elevation 1.")) { Layout = new LayoutStyle { Width = 300 } },
            new Alert("Saved") { Kind = AlertKind.Success, Text = "Your changes are saved.", Layout = new LayoutStyle { Width = 400 } },
            new Box { Layout = new LayoutStyle { Width = 400 }, Children = [new LinearProgress { Value = 0.4f, Label = "Upload" }] },
        ],
    });

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);
}
