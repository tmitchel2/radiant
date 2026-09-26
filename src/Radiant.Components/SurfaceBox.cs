using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>The box that draws a surface: shared by <see cref="Surface"/> and <see cref="PressableSurface"/>.</summary>
internal static class SurfaceBox
{
    public static Box For<T>(T props, ResolvedTheme theme, SurfaceState state)
        where T : IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout
    {
        var outline = props.ShowOutline == true;
        return new Box
        {
            Layout = props.Layout ?? default,
            Background = props.DrawsSurface() ? theme.SurfaceColor(state) : null,
            CornerRadii = theme.Corners(props.CornerShape ?? CornerShapeRole.None),
            Shadows = theme.Elevation(props.Elevation ?? ElevationLevel.Level0),
            BorderWidth = outline ? props.OutlineWidth ?? 1f : 0f,
            BorderColor = props.OutlineColor is { } family ? theme.Get(family)
                : props.OutlineInContentColor == true ? theme.ContentColor(state)
                : props.OutlineVariant == true ? theme.OutlineVariant
                : theme.Outline,
        };
    }
}
