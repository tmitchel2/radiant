using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>
/// The Styles tab: one family of component styles at a time, its sizes and choices first, then a
/// closed section for each of its variants' looks.
/// </summary>
internal static class StyleSections
{
    /// <summary>The families, in the order the chooser lists them.</summary>
    public static IReadOnlyList<string> Families { get; } =
    [
        "Focus and states", "Icons", "Buttons", "Icon buttons", "Chips", "Cards", "Overlays", "Navigation", "Fields", "Selection controls", "Lists", "Showcase", "State layers", "Motion",
    ];

    public static IReadOnlyList<PropertySection> For(ThemeFields fields, int family) => family switch
    {
        0 => Interaction(fields),
        1 => Icons(fields),
        2 => Buttons(fields),
        3 => IconButtons(fields),
        4 => Chips(fields),
        5 => Cards(fields),
        6 => Overlays(fields),
        7 => Navigation(fields),
        8 => Fields(fields),
        9 => Selection(fields),
        10 => Lists(fields),
        11 => Showcase(fields),
        12 => StateLayers(fields),
        _ => Motion(fields),
    };

    private static PropertySection[] Interaction(ThemeFields f)
    {
        var i = ThemeLenses.Interaction;
        return
        [
            new("Focus and states",
            [
                f.Float("Focus ring width", i.Then(s => s.FocusRingWidth, (s, v) => s with { FocusRingWidth = v }), 0f, 6f, 0.5f, "0.0"),
                f.Float("Focus ring gap", i.Then(s => s.FocusRingGap, (s, v) => s with { FocusRingGap = v }), 0f, 6f, 0.5f, "0.0"),
                f.Enum("Focus ring colour", i.Then(s => s.FocusRingColor, (s, v) => s with { FocusRingColor = v })),
                f.Enum("Disabled look", i.Then(s => s.Disabled, (s, v) => s with { Disabled = v })),
                f.Float("Disabled opacity", i.Then(s => s.DisabledOpacity, (s, v) => s with { DisabledOpacity = v }), 0f, 1f, 0.05f, "0.00"),
                f.Enum("State layer", i.Then(s => s.StateLayer, (s, v) => s with { StateLayer = v })),
                f.Float("Press scale", i.Then(s => s.PressScale, (s, v) => s with { PressScale = v }), 0.9f, 1f, 0.01f, "0.00"),
            ]),
        ];
    }

    private static PropertySection[] Icons(ThemeFields f)
    {
        var i = ThemeLenses.Icons;
        return
        [
            new("Icons",
            [
                f.Enum("Icon set", i.Then(s => s.Set, (s, v) => s with { Set = v })),
                f.Float("Size", i.Then(s => s.Size, (s, v) => s with { Size = v }), 16f, 32f),
                f.Float("Weight", i.Then(s => s.Weight, (s, v) => s with { Weight = v }), 100f, 700f, 50f),
                f.Bool("Fill when chosen", i.Then(s => s.FillChosen, (s, v) => s with { FillChosen = v })),
            ]),
        ];
    }

    private static PropertySection[] Buttons(ThemeFields f)
    {
        var b = ThemeLenses.Button;
        return
        [
            new("Buttons",
            [
                f.Float("Height", b.Then(s => s.Height, (s, v) => s with { Height = v }), 24f, 64f),
                f.Float("Padding", b.Then(s => s.Padding, (s, v) => s with { Padding = v }), 4f, 40f),
                f.Float("Text button padding", b.Then(s => s.TextPadding, (s, v) => s with { TextPadding = v }), 4f, 40f),
                f.Float("Icon size", b.Then(s => s.IconSize, (s, v) => s with { IconSize = v }), 12f, 32f),
                f.Enum("Shape", b.Then(s => s.Shape, (s, v) => s with { Shape = v })),
                f.Enum("Label", b.Then(s => s.Label, (s, v) => s with { Label = v })),
                f.Enum("FAB shape", b.Then(s => s.FabShape, (s, v) => s with { FabShape = v })),
                f.Enum("FAB elevation", b.Then(s => s.FabElevation, (s, v) => s with { FabElevation = v })),
            ]),
            f.Look("Filled look", b.Then(s => s.Filled, (s, v) => s with { Filled = v })),
            f.Look("Tonal look", b.Then(s => s.Tonal, (s, v) => s with { Tonal = v })),
            f.Look("Outlined look", b.Then(s => s.Outlined, (s, v) => s with { Outlined = v })),
            f.Look("Text look", b.Then(s => s.Text, (s, v) => s with { Text = v })),
            f.Look("Toggled on look", b.Then(s => s.ToggleOn, (s, v) => s with { ToggleOn = v })),
            f.Look("Elevated look", b.Then(s => s.Elevated, (s, v) => s with { Elevated = v })),
        ];
    }

    private static PropertySection[] IconButtons(ThemeFields f)
    {
        var b = ThemeLenses.IconButton;
        return
        [
            new("Icon buttons",
            [
                f.Float("Size", b.Then(s => s.Size, (s, v) => s with { Size = v }), 24f, 64f),
                f.OptionalFloat("Icon size", b.Then(s => s.IconSize, (s, v) => s with { IconSize = v }), 12f, 40f, 24f),
                f.Enum("Shape", b.Then(s => s.Shape, (s, v) => s with { Shape = v })),
            ]),
            f.Look("Filled look", b.Then(s => s.Filled, (s, v) => s with { Filled = v })),
            f.Look("Tonal look", b.Then(s => s.Tonal, (s, v) => s with { Tonal = v })),
            f.Look("Outlined look", b.Then(s => s.Outlined, (s, v) => s with { Outlined = v })),
            f.Look("Standard look", b.Then(s => s.Standard, (s, v) => s with { Standard = v })),
        ];
    }

    private static PropertySection[] Chips(ThemeFields f)
    {
        var c = ThemeLenses.Chip;
        return
        [
            new("Chips",
            [
                f.Float("Height", c.Then(s => s.Height, (s, v) => s with { Height = v }), 20f, 48f),
                f.Float("Padding", c.Then(s => s.Padding, (s, v) => s with { Padding = v }), 4f, 32f),
                f.Enum("Shape", c.Then(s => s.Shape, (s, v) => s with { Shape = v })),
                f.Enum("Label", c.Then(s => s.Label, (s, v) => s with { Label = v })),
                f.Float("Tag height", c.Then(s => s.TagHeight, (s, v) => s with { TagHeight = v }), 16f, 40f),
                f.Enum("Tag shape", c.Then(s => s.TagShape, (s, v) => s with { TagShape = v })),
                f.Bool("Outlined tags", c.Then(s => s.OutlinedTags, (s, v) => s with { OutlinedTags = v })),
            ]),
            f.Look("At rest look", c.Then(s => s.Rest, (s, v) => s with { Rest = v })),
            f.Look("Chosen look", c.Then(s => s.Chosen, (s, v) => s with { Chosen = v })),
            f.Look("Elevated look", c.Then(s => s.Elevated, (s, v) => s with { Elevated = v })),
        ];
    }

    private static PropertySection[] Cards(ThemeFields f)
    {
        var c = ThemeLenses.Card;
        return
        [
            new("Cards",
            [
                f.Enum("Shape", c.Then(s => s.Shape, (s, v) => s with { Shape = v })),
                f.Float("Padding", c.Then(s => s.Padding, (s, v) => s with { Padding = v }), 0f, 40f),
            ]),
            f.Look("Elevated look", c.Then(s => s.Elevated, (s, v) => s with { Elevated = v })),
            f.Look("Filled look", c.Then(s => s.Filled, (s, v) => s with { Filled = v })),
            f.Look("Outlined look", c.Then(s => s.Outlined, (s, v) => s with { Outlined = v })),
        ];
    }

    private static PropertySection[] Overlays(ThemeFields f)
    {
        var o = ThemeLenses.Overlay;
        return
        [
            new("Menus",
            [
                f.Enum("Shape", o.Then(s => s.MenuShape, (s, v) => s with { MenuShape = v })),
                f.Float("Padding", o.Then(s => s.MenuPadding, (s, v) => s with { MenuPadding = v }), 0f, 16f),
                f.Float("Item inset", o.Then(s => s.MenuItemInset, (s, v) => s with { MenuItemInset = v }), 0f, 16f),
                f.Float("Item height", o.Then(s => s.MenuItemHeight, (s, v) => s with { MenuItemHeight = v }), 24f, 64f),
                f.Enum("Item shape", o.Then(s => s.MenuItemShape, (s, v) => s with { MenuItemShape = v })),
                f.Enum("Item text", o.Then(s => s.MenuItemText, (s, v) => s with { MenuItemText = v })),
                f.OptionalFloat("Icon size", o.Then(s => s.MenuIconSize, (s, v) => s with { MenuIconSize = v }), 12f, 32f, 24f),
            ]),
            new("Dialogs and more",
            [
                f.Enum("Dialog", o.Then(s => s.DialogLook, (s, v) => s with { DialogLook = v })),
                f.Enum("Dialog shape", o.Then(s => s.DialogShape, (s, v) => s with { DialogShape = v })),
                f.Float("Dialog padding", o.Then(s => s.DialogPadding, (s, v) => s with { DialogPadding = v }), 8f, 48f),
                f.Enum("Dialog title", o.Then(s => s.DialogTitle, (s, v) => s with { DialogTitle = v })),
                f.Enum("Snackbar", o.Then(s => s.Snackbar, (s, v) => s with { Snackbar = v })),
                f.Enum("Tooltip shape", o.Then(s => s.TooltipShape, (s, v) => s with { TooltipShape = v })),
                f.Float("Scrim opacity", o.Then(s => s.ScrimOpacity, (s, v) => s with { ScrimOpacity = v }), 0f, 1f, 0.02f, "0.00"),
            ]),
            f.Look("Menu look", o.Then(s => s.Menu, (s, v) => s with { Menu = v })),
            f.Look("Dialog look", o.Then(s => s.Dialog, (s, v) => s with { Dialog = v })),
            f.Look("Popover look", o.Then(s => s.Popover, (s, v) => s with { Popover = v })),
        ];
    }

    private static PropertySection[] Navigation(ThemeFields f)
    {
        var n = ThemeLenses.Navigation;
        return
        [
            new("App bar and tabs",
            [
                f.Float("App bar height", n.Then(s => s.AppBarHeight, (s, v) => s with { AppBarHeight = v }), 40f, 96f),
                f.Enum("App bar title", n.Then(s => s.AppBarTitle, (s, v) => s with { AppBarTitle = v })),
                f.Bool("Tint on scroll", n.Then(s => s.AppBarTonalOnScroll, (s, v) => s with { AppBarTonalOnScroll = v })),
                f.Enum("Tabs", n.Then(s => s.Tabs, (s, v) => s with { Tabs = v })),
                f.Enum("Tab text", n.Then(s => s.TabText, (s, v) => s with { TabText = v })),
            ]),
            new("Drawer",
            [
                f.Enum("Surface", n.Then(s => s.DrawerSurface, (s, v) => s with { DrawerSurface = v })),
                f.Bool("Divider", n.Then(s => s.DrawerDivider, (s, v) => s with { DrawerDivider = v })),
                f.Float("Item height", n.Then(s => s.DrawerItemHeight, (s, v) => s with { DrawerItemHeight = v }), 28f, 64f),
                f.Float("Item padding", n.Then(s => s.DrawerItemPadding, (s, v) => s with { DrawerItemPadding = v }), 4f, 32f),
                f.Enum("Item text", n.Then(s => s.DrawerItemText, (s, v) => s with { DrawerItemText = v })),
                f.OptionalFloat("Icon size", n.Then(s => s.DrawerIconSize, (s, v) => s with { DrawerIconSize = v }), 12f, 32f, 24f),
                f.Enum("Section text", n.Then(s => s.DrawerSectionText, (s, v) => s with { DrawerSectionText = v })),
            ]),
            f.Look("Chosen item look", n.Then(s => s.DrawerChosen, (s, v) => s with { DrawerChosen = v })),
        ];
    }

    private static PropertySection[] Fields(ThemeFields f)
    {
        var d = ThemeLenses.Field;
        return
        [
            new("Fields",
            [
                f.Enum("Look", d.Then(s => s.Look, (s, v) => s with { Look = v })),
                f.Float("Height", d.Then(s => s.Height, (s, v) => s with { Height = v }), 28f, 72f),
                f.Enum("Shape", d.Then(s => s.Shape, (s, v) => s with { Shape = v })),
                f.Enum("Text", d.Then(s => s.Text, (s, v) => s with { Text = v })),
                f.Enum("Label", d.Then(s => s.Label, (s, v) => s with { Label = v })),
                f.Enum("Calendar day", d.Then(s => s.CalendarDay, (s, v) => s with { CalendarDay = v })),
            ]),
        ];
    }

    private static PropertySection[] Selection(ThemeFields f)
    {
        var s = ThemeLenses.Selection;
        return
        [
            new("Selection controls",
            [
                f.Bool("Halo", s.Then(x => x.Halo, (x, v) => x with { Halo = v })),
                f.Float("Checkbox size", s.Then(x => x.CheckboxSize, (x, v) => x with { CheckboxSize = v }), 12f, 28f),
                f.Float("Checkbox radius", s.Then(x => x.CheckboxRadius, (x, v) => x with { CheckboxRadius = v }), 0f, 14f),
                f.Float("Border width", s.Then(x => x.BorderWidth, (x, v) => x with { BorderWidth = v }), 0.5f, 4f, 0.5f, "0.0"),
                f.Float("Radio size", s.Then(x => x.RadioSize, (x, v) => x with { RadioSize = v }), 12f, 28f),
                f.Enum("Switch", s.Then(x => x.Switch, (x, v) => x with { Switch = v })),
                f.Enum("Slider thumb", s.Then(x => x.SliderThumb, (x, v) => x with { SliderThumb = v })),
                f.Float("Track thickness", s.Then(x => x.TrackThickness, (x, v) => x with { TrackThickness = v }), 2f, 16f),
                f.Enum("Track", s.Then(x => x.Track, (x, v) => x with { Track = v })),
                f.Bool("Track container colour", s.Then(x => x.TrackContainer, (x, v) => x with { TrackContainer = v })),
                f.Enum("Label", s.Then(x => x.Label, (x, v) => x with { Label = v })),
            ]),
        ];
    }

    private static PropertySection[] Lists(ThemeFields f)
    {
        var l = ThemeLenses.List;
        return
        [
            new("Lists",
            [
                f.Float("One-line height", l.Then(s => s.OneLineHeight, (s, v) => s with { OneLineHeight = v }), 32f, 72f),
                f.Float("Line step", l.Then(s => s.LineStep, (s, v) => s with { LineStep = v }), 8f, 32f),
                f.Float("Padding", l.Then(s => s.Padding, (s, v) => s with { Padding = v }), 0f, 32f),
                f.Enum("Headline", l.Then(s => s.Headline, (s, v) => s with { Headline = v })),
                f.Enum("Supporting text", l.Then(s => s.Supporting, (s, v) => s with { Supporting = v })),
                f.Enum("Shape", l.Then(s => s.Shape, (s, v) => s with { Shape = v })),
                f.Float("Accordion header height", l.Then(s => s.AccordionHeaderHeight, (s, v) => s with { AccordionHeaderHeight = v }), 32f, 72f),
                f.Enum("Accordion title", l.Then(s => s.AccordionTitle, (s, v) => s with { AccordionTitle = v })),
            ]),
            f.Look("Selected look", l.Then(s => s.Selected, (s, v) => s with { Selected = v })),
        ];
    }

    private static PropertySection[] Showcase(ThemeFields f)
    {
        var s = ThemeLenses.Showcase;
        return
        [
            new("Showcase",
            [
                f.Enum("Panel shape", s.Then(x => x.PanelShape, (x, v) => x with { PanelShape = v })),
                f.Enum("Empty state icon shape", s.Then(x => x.EmptyIconShape, (x, v) => x with { EmptyIconShape = v })),
            ]),
            f.Look("Hero look", s.Then(x => x.Hero, (x, v) => x with { Hero = v })),
            f.Look("Call to action look", s.Then(x => x.CallToAction, (x, v) => x with { CallToAction = v })),
            f.Look("Feature icon look", s.Then(x => x.FeatureIcon, (x, v) => x with { FeatureIcon = v })),
            f.Look("Featured tier look", s.Then(x => x.FeaturedTier, (x, v) => x with { FeaturedTier = v })),
            f.Look("Empty state icon look", s.Then(x => x.EmptyIcon, (x, v) => x with { EmptyIcon = v })),
        ];
    }

    private static PropertySection[] StateLayers(ThemeFields f)
    {
        var l = ThemeLenses.StateLayers;
        PropertyItem Opacity(string name, Func<StateLayerOpacities, float> get, Func<StateLayerOpacities, float, StateLayerOpacities> set) =>
            f.Float(name, l.Then(get, set), 0f, 0.5f, 0.01f, "0.00");
        return
        [
            new("State layers",
            [
                Opacity("Hover", s => s.Hover, (s, v) => s with { Hover = v }),
                Opacity("Focus", s => s.Focus, (s, v) => s with { Focus = v }),
                Opacity("Pressed", s => s.Pressed, (s, v) => s with { Pressed = v }),
                Opacity("Dragged", s => s.Dragged, (s, v) => s with { Dragged = v }),
                Opacity("Disabled container", s => s.DisabledContainer, (s, v) => s with { DisabledContainer = v }),
                Opacity("Disabled content", s => s.DisabledContent, (s, v) => s with { DisabledContent = v }),
            ]),
        ];
    }

    private static PropertySection[] Motion(ThemeFields f)
    {
        var m = ThemeLenses.Motion;
        PropertyItem Duration(string name, Func<MotionScheme, TimeSpan> get, Func<MotionScheme, TimeSpan, MotionScheme> set) =>
            f.Float(name, m.Then(get, set).Map(d => (float)d.TotalMilliseconds, ms => TimeSpan.FromMilliseconds(ms)), 0f, 1000f, 25f, "0 ms");
        return
        [
            new("Motion",
            [
                Duration("Short", s => s.ShortDuration, (s, v) => s with { ShortDuration = v }),
                Duration("Medium", s => s.MediumDuration, (s, v) => s with { MediumDuration = v }),
                Duration("Long", s => s.LongDuration, (s, v) => s with { LongDuration = v }),
                f.Bool("Reduced", m.Then(s => s.Reduced, (s, v) => s with { Reduced = v })),
            ]),
        ];
    }
}
