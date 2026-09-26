using System;
using Radiant.Theming;

namespace Radiant.Gallery.ThemeLab;

/// <summary>Where the editor's properties start: a theme's parts, and each family of component styles.</summary>
internal static class ThemeLenses
{
    public static ThemeLens<Theme> Theme { get; } = new(theme => theme, (_, value) => value);

    public static ThemeLens<ThemeColors> Colors { get; } = new(theme => theme.Colors, (theme, value) => theme with { Colors = value });

    /// <summary>The hand-picked palette showing: light or dark, whichever the theme is in.</summary>
    public static ThemeLens<ColorRoles> Roles { get; } = new(
        theme => theme.Colors.Roles ?? throw new InvalidOperationException("The theme's colours are worked out from a seed, not hand-picked."),
        (theme, value) => theme with { Colors = theme.Colors.IsDark ? theme.Colors with { Dark = value } : theme.Colors with { Light = value } });

    public static ThemeLens<ShapeScale> Shape { get; } = new(theme => theme.Shape, (theme, value) => theme with { Shape = value });

    public static ThemeLens<StateLayerOpacities> StateLayers { get; } = new(theme => theme.StateLayers, (theme, value) => theme with { StateLayers = value });

    public static ThemeLens<MotionScheme> Motion { get; } = new(theme => theme.Motion, (theme, value) => theme with { Motion = value });

    public static ThemeLens<ComponentStyles> Components { get; } = new(theme => theme.Components, (theme, value) => theme with { Components = value });

    public static ThemeLens<InteractionStyle> Interaction { get; } = Components.Then(c => c.Interaction, (c, v) => c with { Interaction = v });

    public static ThemeLens<IconStyle> Icons { get; } = Components.Then(c => c.Icons, (c, v) => c with { Icons = v });

    public static ThemeLens<ButtonStyle> Button { get; } = Components.Then(c => c.Button, (c, v) => c with { Button = v });

    public static ThemeLens<IconButtonStyle> IconButton { get; } = Components.Then(c => c.IconButton, (c, v) => c with { IconButton = v });

    public static ThemeLens<ChipStyle> Chip { get; } = Components.Then(c => c.Chip, (c, v) => c with { Chip = v });

    public static ThemeLens<CardStyle> Card { get; } = Components.Then(c => c.Card, (c, v) => c with { Card = v });

    public static ThemeLens<OverlayStyle> Overlay { get; } = Components.Then(c => c.Overlay, (c, v) => c with { Overlay = v });

    public static ThemeLens<NavigationStyle> Navigation { get; } = Components.Then(c => c.Navigation, (c, v) => c with { Navigation = v });

    public static ThemeLens<FieldStyle> Field { get; } = Components.Then(c => c.Field, (c, v) => c with { Field = v });

    public static ThemeLens<SelectionStyle> Selection { get; } = Components.Then(c => c.Selection, (c, v) => c with { Selection = v });

    public static ThemeLens<ListStyle> List { get; } = Components.Then(c => c.List, (c, v) => c with { List = v });

    public static ThemeLens<ShowcaseStyle> Showcase { get; } = Components.Then(c => c.Showcase, (c, v) => c with { Showcase = v });
}
