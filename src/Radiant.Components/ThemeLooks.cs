using Radiant.Theming;

namespace Radiant.Components;

/// <summary>Questions components ask of the theme's component styles.</summary>
internal static class ThemeLooks
{
    /// <summary>Whether a disabled control is recoloured (rather than faded whole, in its usual colours).</summary>
    public static bool RecolorsDisabled(this ResolvedTheme theme) =>
        theme.Theme.Components.Interaction.Disabled == DisabledLook.Recolor;

    /// <summary>The tray segmented tabs and buttons sit in: the surface under it, tinted a little towards its content.</summary>
    public static Radiant.Graphics2D.Color SegmentTray(this ResolvedTheme theme, SurfaceState surface) =>
        theme.StateLayerColor(surface, 0.06f);

    /// <summary>The raised pill of a chosen segment: white on a light theme, a lighter tint of the surface on a dark one.</summary>
    public static Radiant.Graphics2D.Color SegmentPill(this ResolvedTheme theme, SurfaceState surface) =>
        theme.Theme.Colors.IsDark ? theme.StateLayerColor(surface, 0.14f) : theme.Get(SurfaceName.SurfaceContainerLowest);
}
