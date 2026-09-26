using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>Theme hooks for components.</summary>
public static class ThemeHooks
{
    /// <summary>The theme in force; the component is rebuilt when it changes.</summary>
    public static ResolvedTheme UseTheme(this BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        return context.Use(ThemeContexts.Theme);
    }

    /// <summary>The surface state in force here; the component is rebuilt when it changes.</summary>
    public static SurfaceState UseSurface(this BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        return context.Use(ThemeContexts.Surface);
    }
}
