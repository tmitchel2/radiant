using Radiant.Theming;

namespace Radiant.Components;

/// <summary>Turns a style's <see cref="SurfaceLook"/> into the props of the surface that draws it.</summary>
internal static class SurfaceLooks
{
    public static PressableSurface Pressable(SurfaceLook look) => new()
    {
        SurfaceColor = look.Surface,
        SurfaceContainerToggle = look.SurfaceContainer ? true : null,
        ContentColor = look.Content,
        ContentOnToggle = look.ContentOn ? true : null,
        ContentLegibility = look.ContentLegibility,
        ShowOutline = look.Outline ? true : null,
        OutlineVariant = look.OutlineVariant ? true : null,
        Elevation = look.Elevation,
    };

    public static Surface Surface(SurfaceLook look) => new()
    {
        SurfaceColor = look.Surface,
        SurfaceContainerToggle = look.SurfaceContainer ? true : null,
        ContentColor = look.Content,
        ContentOnToggle = look.ContentOn ? true : null,
        ContentLegibility = look.ContentLegibility,
        ShowOutline = look.Outline ? true : null,
        OutlineVariant = look.OutlineVariant ? true : null,
        Elevation = look.Elevation,
    };
}
