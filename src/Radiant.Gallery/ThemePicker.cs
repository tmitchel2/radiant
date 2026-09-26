using System;
using Radiant.Components;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>
/// The app bar's theme button, which opens the theme page: presets, light or dark, and every
/// parameter to edit. Also the gallery's ways of changing theme, shared by the menus and the page.
/// </summary>
/// <param name="OnOpen">Opens the theme page.</param>
internal sealed partial record ThemePicker(Action OnOpen) : Component
{
    [TestId<IconButton>] public static partial string Button { get; }

    /// <summary>How long a change of theme animates for.</summary>
    public static TimeSpan Transition { get; } = TimeSpan.FromMilliseconds(400);

    public override Element? Build(BuildContext context) =>
        new Tooltip("Theme", new IconButton("palette", "Theme") { TestId = Button, OnPress = OnOpen });

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
