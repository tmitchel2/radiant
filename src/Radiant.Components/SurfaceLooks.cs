using Radiant.Theming;

namespace Radiant.Components;

/// <summary>
/// Turns a style's <see cref="SurfaceLook"/> into the props of the surface that draws it, for
/// components and templates that take their looks from the theme's component styles.
/// </summary>
public static class SurfaceLooks
{
    /// <summary>A pressable surface drawn in <paramref name="look"/>.</summary>
    public static PressableSurface Pressable(SurfaceLook look) => new()
    {
        SurfaceColor = look.Surface,
        SurfaceContainerToggle = look.SurfaceContainer ? true : null,
        ContentColor = look.Content,
        ContentOnToggle = look.ContentOn ? true : null,
        ContentLegibility = look.ContentLegibility,
        ShowOutline = look.Outline ? true : null,
        OutlineVariant = look.OutlineVariant ? true : null,
        OutlineWidth = look.OutlineWidth,
        OutlineColor = look.OutlineColor,
        Elevation = look.Elevation,
    };

    /// <summary>A surface drawn in <paramref name="look"/>.</summary>
    public static Surface Surface(SurfaceLook look) => new()
    {
        SurfaceColor = look.Surface,
        SurfaceContainerToggle = look.SurfaceContainer ? true : null,
        ContentColor = look.Content,
        ContentOnToggle = look.ContentOn ? true : null,
        ContentLegibility = look.ContentLegibility,
        ShowOutline = look.Outline ? true : null,
        OutlineVariant = look.OutlineVariant ? true : null,
        OutlineWidth = look.OutlineWidth,
        OutlineColor = look.OutlineColor,
        Elevation = look.Elevation,
    };
}
