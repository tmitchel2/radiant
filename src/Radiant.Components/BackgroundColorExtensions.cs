using Radiant.Theming;

namespace Radiant.Components;

/// <summary>Turns background-colour props into what the theme works with.</summary>
public static class BackgroundColorExtensions
{
    /// <summary>The surface change these props make.</summary>
    public static SurfaceChange ToSurfaceChange(this IHasBackgroundColor props)
    {
        System.ArgumentNullException.ThrowIfNull(props);
        return new SurfaceChange
        {
            Surface = props.SurfaceColor,
            SurfaceLegibility = props.SurfaceLegibility,
            ToggleSurfaceOn = props.SurfaceOnToggle ?? false,
            ToggleSurfaceContainer = props.SurfaceContainerToggle ?? false,
            Content = props.ContentColor,
            ContentLegibility = props.ContentLegibility,
            ToggleContentOn = props.ContentOnToggle ?? false,
            ToggleContentContainer = props.ContentContainerToggle ?? false,
            ContentFocused = props.ContentFocusedColor,
            ShowError = props.ShowError ?? false,
            ShowDisabled = props.ShowDisabled ?? false,
        };
    }

    /// <summary>Whether the surface colour is drawn: as asked, or when the props change the surface.</summary>
    public static bool DrawsSurface(this IHasBackgroundColor props)
    {
        System.ArgumentNullException.ThrowIfNull(props);
        return props.ShowSurface ?? (props.SurfaceColor is not null || props.SurfaceOnToggle == true || props.SurfaceContainerToggle == true);
    }
}
