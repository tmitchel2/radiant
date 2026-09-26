using System;
using Radiant.Components;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>The theme's showcase looks (<see cref="ShowcaseStyle"/>) as surfaces for the templates to fill.</summary>
internal static class Showcase
{
    /// <summary>A hero's or call to action's panel: the look <paramref name="pick"/> chooses, with the showcase's panel corners.</summary>
    public static Surface Panel(BuildContext context, Func<ShowcaseStyle, SurfaceLook> pick)
    {
        var style = context.UseTheme().Theme.Components.Showcase;
        return SurfaceLooks.Surface(pick(style)) with { CornerShape = style.PanelShape };
    }
}
