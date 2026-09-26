using System;
using System.Collections.Generic;
using Radiant.Components;
using Radiant.Components.Primitives;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>
/// The app bar's theme button: a menu of the theme presets, the current one ticked, and light or
/// dark. A choice animates the whole gallery to it, keeping light or dark and the accent.
/// </summary>
internal sealed partial record ThemePicker(ThemeController Themes) : Component
{
    [TestId<IconButton>] public static partial string Button { get; }

    /// <summary>How long a change of theme animates for.</summary>
    public static TimeSpan Transition { get; } = TimeSpan.FromMilliseconds(400);

    public override Element? Build(BuildContext context)
    {
        var open = context.UseState(false);
        var anchor = context.UseRef(new ElementRef()).Value;
        var theme = context.UseTheme().Theme;
        var themes = Themes;
        var items = new List<MenuItem>();
        foreach (var preset in ThemePresets.All)
        {
            items.Add(new MenuItem(preset.Name!, () => Choose(themes, preset))
            {
                Icon = preset.Name == theme.Name ? "check" : null,
            });
        }
        var dark = theme.Colors.IsDark;
        items.Add(new MenuItem(dark ? "Light" : "Dark", () => ToggleDark(themes))
        {
            Icon = dark ? "light_mode" : "dark_mode",
            DividerBefore = true,
        });
        return new Fragment(
            new Box
            {
                Ref = anchor,
                Children = [new Tooltip("Theme", new IconButton("palette", "Theme") { TestId = Button, OnPress = () => open.Set(true) })],
            },
            new Menu(anchor, open.Value, () => open.Set(false), items) { Align = SideAlign.End });
    }

    /// <summary>Switches to a preset, keeping the user's light or dark, contrast, accent and motion.</summary>
    public static void Choose(ThemeController themes, Theme preset)
    {
        ArgumentNullException.ThrowIfNull(themes);
        themes.Set(themes.Theme.WithStyle(preset), Transition);
    }

    /// <summary>Swaps light and dark.</summary>
    public static void ToggleDark(ThemeController themes)
    {
        ArgumentNullException.ThrowIfNull(themes);
        themes.Set(themes.Theme with { Colors = themes.Theme.Colors with { IsDark = !themes.Theme.Colors.IsDark } }, TimeSpan.FromMilliseconds(300));
    }
}
