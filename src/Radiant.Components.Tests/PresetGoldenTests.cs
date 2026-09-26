using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Testing;
using Radiant.Theming;
using Radiant.UI.Core;
using static Radiant.Components.Tests.GoldenSheets;

namespace Radiant.Components.Tests;

/// <summary>
/// The same components in each theme preset but the default (whose sheets are the other goldens),
/// light and dark: colour, corners, type, shadows and the focus ring all come from the preset; and
/// the structures that component styles choose between (tabs, fields, switches, slider thumbs) in
/// every preset.
/// </summary>
[TestClass]
[TestCategory("Gpu")]
public class PresetGoldenTests
{
    public static System.Collections.Generic.IEnumerable<object[]> Presets =>
        ThemePresets.All.Where(p => p != ThemePresets.Tonal).Select(p => new object[] { p.Name! });

    public static System.Collections.Generic.IEnumerable<object[]> AllPresets => ThemePresets.All.Select(p => new object[] { p.Name! });

    [TestMethod]
    [DynamicData(nameof(AllPresets))]
    public void Structures(string preset) => InPreset(preset, "Structures", 640, 560, () => Column(16,
        new Box
        {
            Layout = new LayoutStyle { Width = 420 },
            Children = [new Tabs([new Tab("Model") { Icon = "view_in_ar" }, new Tab("Manual") { Icon = "menu_book" }, new Tab("Parts") { Icon = "category" }], 1, _ => { })],
        },
        new Box
        {
            Layout = new LayoutStyle { Width = 320 },
            Children = [new SegmentedButton([new Segment("Change it"), new Segment("Start again")], new System.Collections.Generic.HashSet<int> { 0 }, _ => { })],
        },
        Row(
            new TextField("Name") { Variant = TextFieldVariant.Outlined, InitialText = "Alpine Chalet", SupportingText = "Shown on the cover", Layout = new LayoutStyle { Width = 220 } },
            new TextField("Code") { Variant = TextFieldVariant.Outlined, InitialText = "12345", Error = "That code has expired", Layout = new LayoutStyle { Width = 220 } }),
        Row(
            new Card(new TextField("Prompt") { Variant = TextFieldVariant.Plain, Placeholder = "Describe what to build…" }) { Variant = CardVariant.Outlined, Layout = new LayoutStyle { Width = 260, Padding = Edges.All(12) } },
            new SurfaceText("Try one") { TextType = TextType.Overline, Legibility = Legibility.Medium },
            new Badge(new SurfaceText("Versions")) { Inline = true, Count = 3 },
            new Tag("611 pieces")),
        Row(
            new Box { Layout = new LayoutStyle { Width = 240 }, Children = [new Slider(0.4f, _ => { }) { Label = "Volume" }] },
            new Box { Layout = new LayoutStyle { Width = 160 }, Children = [new LinearProgress { Value = 0.3f, Label = "Progress" }] },
            new ToggleButton("sync", "Spin", true, _ => { }) { ShowLabel = true },
            new ToggleButton("stop", "Stop", false, _ => { }) { ShowLabel = true }),
        Row(
            new Checkbox(false, _ => { }) { Label = "Off" },
            new Checkbox(true, null) { Label = "Disabled", Disabled = true },
            new Radio(false, () => { }) { Label = "Other" },
            new Switch(false, _ => { }) { AccessibleLabel = "Off" },
            new Switch(true, null) { AccessibleLabel = "Disabled", Disabled = true })));

    [TestMethod]
    [DynamicData(nameof(Presets))]
    public void Controls(string preset) => InPreset(preset, "Controls", 640, 420, () => Column(12,
        Row(
            new SurfaceButton("Filled"),
            new SurfaceButton("Tonal", ButtonVariant.Tonal),
            new SurfaceButton("Outlined", ButtonVariant.Outlined) { Icon = "add" },
            new SurfaceButton("Text", ButtonVariant.Text),
            new SurfaceButton("Elevated", ButtonVariant.Elevated),
            new SurfaceButton("Disabled") { ShowDisabled = true }),
        Row(
            new IconButton("favorite", "Favourite", IconButtonVariant.Filled),
            new IconButton("settings", "Settings", IconButtonVariant.Tonal),
            new IconButton("delete", "Delete", IconButtonVariant.Outlined),
            new IconButton("search", "Search"),
            new Checkbox(true, _ => { }) { Label = "On" },
            new Switch(true, _ => { }) { AccessibleLabel = "On" },
            new Radio(true, () => { }) { Label = "Chosen" }),
        Row(
            new Chip("Assist") { Icon = "event" },
            new Chip("Chosen") { Selected = true, OnPress = () => { } },
            new Badge(new SurfaceIcon("notifications")) { Count = 3 },
            // Segments share the width they're given, so the group needs one.
            new Box
            {
                Layout = new LayoutStyle { Width = 260 },
                Children = [new SegmentedButton([new Segment("Day"), new Segment("Week"), new Segment("Month")], new System.Collections.Generic.HashSet<int> { 1 }, _ => { })],
            }),
        Row(
            new TextField("Email") { Variant = TextFieldVariant.Outlined, InitialText = "tom@example.com", Layout = new LayoutStyle { Width = 240 } },
            new SearchField("Search") { Layout = new LayoutStyle { Width = 200 } }),
        new Alert("Saved") { Kind = AlertKind.Success, Text = "Your changes are live." }));

    [TestMethod]
    [DynamicData(nameof(Presets))]
    public void TypeAndSurfaces(string preset) => InPreset(preset, "TypeAndSurfaces", 640, 400, () => Column(12,
        new SurfaceText("Display small") { TextType = TextType.DisplaySmall },
        new SurfaceText("Headline medium") { TextType = TextType.HeadlineMedium },
        new SurfaceText("Title large, then body text below it") { TextType = TextType.TitleLarge },
        new SurfaceText("Body medium: the quick brown fox jumps over the lazy dog.") { TextType = TextType.BodyMedium },
        new SurfaceText("Supporting text") { TextType = TextType.BodySmall, Legibility = Legibility.Medium },
        Row(
            new Card(new SurfaceText("Elevated card")),
            new Card(new SurfaceText("Filled card")) { Variant = CardVariant.Filled },
            new Card(new SurfaceText("Outlined card")) { Variant = CardVariant.Outlined },
            new Card(new SurfaceText("Primary container")) { SurfaceColor = SurfaceName.Primary, SurfaceContainerToggle = true })));

    [TestMethod]
    [DynamicData(nameof(Presets))]
    public void FocusRing(string preset)
    {
        foreach (var dark in new[] { false, true })
        {
            using var canvas = Canvas(160, 72);
            var snapshot = UISnapshot.Render(canvas, Themed(Theme(preset, dark), new SurfaceButton("Focused")), act: root => root.MoveFocus(forward: true));
            Golden.AssertMatches(snapshot, $"Preset_{preset}_FocusRing_{(dark ? "dark" : "light")}");
        }
    }

    private static Theme Theme(string preset, bool dark)
    {
        var theme = ThemePresets.Find(preset)!;
        return theme with { Colors = theme.Colors with { IsDark = dark } };
    }

    private static void InPreset(string preset, string name, int width, int height, Func<Element> sheet)
    {
        foreach (var dark in new[] { false, true })
        {
            CheckIn(Theme(preset, dark), $"Preset_{preset}_{name}_{(dark ? "dark" : "light")}", width, height, sheet);
        }
    }
}
