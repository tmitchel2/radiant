using System;
using Radiant.ColorSystem;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;
using Color = Radiant.Graphics2D.Color;

namespace Radiant.Gallery;

/// <summary>
/// The first milestone: a card with filled, tonal and outlined buttons, and a button that shuffles
/// the theme (seed, variant, light or dark, corner scale), animating everything to it.
/// </summary>
internal sealed record VerticalSlice(ThemeController Themes) : Component
{
    /// <summary>Open the dialog on the first frame (for snapshots).</summary>
    public bool StartWithDialog { get; init; }

    /// <summary>Open the menu on the first frame (for snapshots).</summary>
    public bool StartWithMenu { get; init; }

    private static readonly Variant[] s_variants = [Variant.TonalSpot, Variant.Vibrant, Variant.Expressive, Variant.Fidelity, Variant.Content, Variant.Neutral];
    private static readonly float[] s_corners = [0f, 0.5f, 1f, 1.5f, 2f];

    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        var presses = context.UseState(0);
        var agreed = context.UseState(true);
        var notify = context.UseState(false);
        var wifi = context.UseState(true);
        var size = context.UseState("Medium");
        var dialog = context.UseState(StartWithDialog);
        var menu = context.UseState(StartWithMenu);
        var menuAnchor = context.UseRef(new ElementRef()).Value;
        var last = context.UseState("nothing yet");
        var themes = Themes;
        return new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(32), RowGap = 24 },
            Background = theme.Background,
            Children =
            [
                new SurfaceText("Radiant") { TextType = TextType.DisplaySmall },
                new Card(
                    new SurfaceText("Vertical slice") { TextType = TextType.TitleLarge },
                    new SurfaceText("A card on elevation 1 with each kind of button. Shuffle the theme and every colour, "
                        + "corner and shadow follows, animated.") { Legibility = Legibility.Medium },
                    new Row(
                        new SurfaceButton("Filled") { OnPress = () => presses.Update(n => n + 1) },
                        new SurfaceButton("Tonal", ButtonVariant.Tonal),
                        new SurfaceButton("Outlined", ButtonVariant.Outlined),
                        new SurfaceButton("Text", ButtonVariant.Text),
                        new SurfaceButton("Elevated", ButtonVariant.Elevated)) { Gap = 8 },
                    new SurfaceText($"Filled pressed {presses.Value} times") { TextType = TextType.LabelMedium, Legibility = Legibility.Medium })
                {
                    Layout = new LayoutStyle { MaxWidth = 640, Padding = Edges.All(20), RowGap = 12 },
                },
                new Row(
                    new SurfaceButton("Shuffle theme", ButtonVariant.Tonal) { Icon = "palette", OnPress = () => Shuffle(themes) },
                    new SurfaceButton("Error") { SurfaceColor = SurfaceName.Error },
                    new SurfaceButton("Success") { SurfaceColor = SurfaceName.Success },
                    new SurfaceButton("Disabled") { ShowDisabled = true }),
                new Row(
                    new SurfaceButton("Add", ButtonVariant.Filled) { Icon = "add" },
                    new SurfaceButton("Download", ButtonVariant.Outlined) { Icon = "download" },
                    new IconButton("search", "Search"),
                    new IconButton("favorite", "Favourite", IconButtonVariant.Filled) { IconFilled = true },
                    new IconButton("settings", "Settings", IconButtonVariant.Tonal),
                    new IconButton("delete", "Delete", IconButtonVariant.Outlined)),
                new Row(
                    new Checkbox(agreed.Value, agreed.Set) { Label = "I agree" },
                    new Checkbox(notify.Value, notify.Set) { Label = "Notify me" },
                    new Checkbox(false, null) { Label = "Some", Indeterminate = true },
                    new Switch(wifi.Value, wifi.Set) { Label = "Wi-Fi" },
                    new Switch(!wifi.Value, v => wifi.Set(!v)),
                    new Radio(size.Value == "Small", () => size.Set("Small")) { Label = "Small" },
                    new Radio(size.Value == "Medium", () => size.Set("Medium")) { Label = "Medium" },
                    new Checkbox(true, null) { Label = "Disabled", Disabled = true }) { Gap = 12 },
                new Row(
                    new SurfaceButton("Open dialog", ButtonVariant.Outlined) { Icon = "open_in_new", OnPress = () => dialog.Set(true) },
                    new Box
                    {
                        Ref = menuAnchor,
                        Children = [new SurfaceButton("Menu", ButtonVariant.Tonal) { Icon = "menu", OnPress = () => menu.Set(true) }],
                    },
                    new Tooltip("Tooltips wait 600 ms", new IconButton("info", "About tooltips")),
                    new SurfaceText($"Last menu choice: {last.Value}") { Legibility = Legibility.Medium }),
                new Card(
                    new ListItem("Inbox") { LeadingIcon = "inbox", TrailingText = "24", OnPress = () => { }, Selected = true },
                    new ListItem("Starred") { LeadingIcon = "star", SupportingText = "Messages you marked", OnPress = () => { } },
                    new Divider(),
                    new ListItem("Archive") { LeadingIcon = "archive", TrailingIcon = "chevron_right", OnPress = () => { } })
                {
                    Variant = CardVariant.Outlined,
                    Layout = new LayoutStyle { MaxWidth = 360, Padding = Edges.Symmetric(0, 8) },
                },
                new Menu(menuAnchor, menu.Value, () => menu.Set(false),
                [
                    new MenuItem("Copy", () => last.Set("Copy")) { Icon = "content_copy", Shortcut = "⌘C" },
                    new MenuItem("Paste", () => last.Set("Paste")) { Icon = "content_paste", Shortcut = "⌘V" },
                    new MenuItem("Delete", () => last.Set("Delete")) { Icon = "delete", DividerBefore = true },
                    new MenuItem("Unavailable") { Icon = "block", Disabled = true },
                ]),
                new Dialog(dialog.Value, () => dialog.Set(false))
                {
                    Icon = "delete",
                    Title = "Delete this item?",
                    Text = "It will be gone for good. The dialog traps focus, closes on Escape or a press outside, and animates.",
                    Actions =
                    [
                        new SurfaceButton("Cancel", ButtonVariant.Text) { OnPress = () => dialog.Set(false) },
                        new SurfaceButton("Delete", ButtonVariant.Text) { OnPress = () => dialog.Set(false) },
                    ],
                },
                new Row(
                    new Card(new SurfaceText("Filled card")) { Variant = CardVariant.Filled },
                    new Card(new SurfaceText("Outlined card")) { Variant = CardVariant.Outlined },
                    new Card(new SurfaceText("Primary container")) { SurfaceColor = SurfaceName.Primary, SurfaceContainerToggle = true }) { Gap = 16 },
            ],
        };
    }

    private static void Shuffle(ThemeController themes)
    {
        var random = Random.Shared;
        var seed = Hct.From(random.NextDouble() * 360, 48 + random.NextDouble() * 40, 50).ToInt();
        var current = themes.Theme;
        themes.Set(current with
        {
            Colors = current.Colors with
            {
                Seed = Color.FromArgb(seed),
                Variant = s_variants[random.Next(s_variants.Length)],
                IsDark = random.Next(2) == 0,
            },
            Shape = new ShapeScale().Scaled(s_corners[random.Next(s_corners.Length)]),
        }, TimeSpan.FromMilliseconds(400));
    }
}
