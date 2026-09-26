namespace Radiant.Theming;

/// <summary>How the app bar, navigation drawer and tabs are drawn.</summary>
public sealed record NavigationStyle
{
    /// <summary>The app bar's height, in pixels.</summary>
    public float AppBarHeight { get; init; } = 64f;

    /// <summary>The app bar's title.</summary>
    public TextType AppBarTitle { get; init; } = TextType.TitleLarge;

    /// <summary>Whether the app bar takes the container colour when content scrolls under it (otherwise it keeps the surface and shows a line below).</summary>
    public bool AppBarTonalOnScroll { get; init; } = true;

    /// <summary>The drawer's surface.</summary>
    public SurfaceName DrawerSurface { get; init; } = SurfaceName.SurfaceContainerLow;

    /// <summary>Whether a line divides the drawer from the page.</summary>
    public bool DrawerDivider { get; init; }

    /// <summary>A drawer row's height, in pixels.</summary>
    public float DrawerItemHeight { get; init; } = 56f;

    /// <summary>A drawer row's padding at its start, in pixels (the end has half as much again).</summary>
    public float DrawerItemPadding { get; init; } = 16f;

    /// <summary>A drawer row's label.</summary>
    public TextType DrawerItemText { get; init; } = TextType.LabelLarge;

    /// <summary>A drawer row's icon size, in pixels; the theme's icon size if null.</summary>
    public float? DrawerIconSize { get; init; }

    /// <summary>The drawer's section headings.</summary>
    public TextType DrawerSectionText { get; init; } = TextType.TitleSmall;

    /// <summary>The current drawer row.</summary>
    public SurfaceLook DrawerChosen { get; init; } = new() { Surface = SurfaceName.Secondary, SurfaceContainer = true };

    /// <summary>How tabs are built.</summary>
    public TabsLook Tabs { get; init; } = TabsLook.Underline;

    /// <summary>A tab's label.</summary>
    public TextType TabText { get; init; } = TextType.TitleSmall;
}
