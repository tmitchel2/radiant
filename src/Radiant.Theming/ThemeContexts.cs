using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>The contexts the theme flows down the tree in.</summary>
public static class ThemeContexts
{
    /// <summary>The resolved theme; the default theme where nothing provides one.</summary>
    public static Context<ResolvedTheme> Theme { get; } = new(ResolvedTheme.Default);

    /// <summary>The surface state at this point; the root state where no surface has changed it.</summary>
    public static Context<SurfaceState> Surface { get; } = new(SurfaceState.Default);
}
