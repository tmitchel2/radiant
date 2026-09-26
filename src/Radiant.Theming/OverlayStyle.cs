namespace Radiant.Theming;

/// <summary>How menus, dialogs, popovers and tooltips are drawn.</summary>
public sealed record OverlayStyle
{
    /// <summary>A menu's panel.</summary>
    public SurfaceLook Menu { get; init; } = new() { Surface = SurfaceName.SurfaceContainer, Elevation = ElevationLevel.Level2 };

    /// <summary>A menu's corners.</summary>
    public CornerShapeRole MenuShape { get; init; } = CornerShapeRole.ExtraSmall;

    /// <summary>The padding round a menu's items, in pixels: above and below, and at the sides (where items are inset and rounded).</summary>
    public float MenuPadding { get; init; } = 8f;

    /// <summary>How far items are inset from a menu's sides, in pixels; inset items have rounded highlights.</summary>
    public float MenuItemInset { get; init; }

    /// <summary>A menu item's height at standard density, in pixels.</summary>
    public float MenuItemHeight { get; init; } = 48f;

    /// <summary>A menu item's corners (seen when it's highlighted).</summary>
    public CornerShapeRole MenuItemShape { get; init; } = CornerShapeRole.None;

    /// <summary>A menu item's text.</summary>
    public TextType MenuItemText { get; init; } = TextType.LabelLarge;

    /// <summary>A menu item's icon size, in pixels; the theme's icon size if null.</summary>
    public float? MenuIconSize { get; init; }

    /// <summary>A dialog's panel.</summary>
    public SurfaceLook Dialog { get; init; } = new() { Surface = SurfaceName.SurfaceContainerHigh, Elevation = ElevationLevel.Level3 };

    /// <summary>A dialog's corners.</summary>
    public CornerShapeRole DialogShape { get; init; } = CornerShapeRole.ExtraLarge;

    /// <summary>A dialog's padding, in pixels.</summary>
    public float DialogPadding { get; init; } = 24f;

    /// <summary>A dialog's title.</summary>
    public TextType DialogTitle { get; init; } = TextType.HeadlineSmall;

    /// <summary>How a dialog is laid out.</summary>
    public DialogLook DialogLook { get; init; } = DialogLook.Headline;

    /// <summary>How brief messages are shown.</summary>
    public SnackbarLook Snackbar { get; init; } = SnackbarLook.Bar;

    /// <summary>A tooltip's corners.</summary>
    public CornerShapeRole TooltipShape { get; init; } = CornerShapeRole.ExtraSmall;

    /// <summary>A popover's panel.</summary>
    public SurfaceLook Popover { get; init; } = new() { Surface = SurfaceName.SurfaceContainerHigh, Elevation = ElevationLevel.Level2 };

    /// <summary>How opaque the scrim behind a modal is.</summary>
    public float ScrimOpacity { get; init; } = 0.32f;
}
