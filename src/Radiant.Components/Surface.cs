using System.Collections.Generic;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A region painted as a surface: it applies its background-colour props to the surface state it
/// inherits, draws that surface (colour, corners, elevation shadow, outline) and hands the new
/// state to its children, so text and icons inside know what is readable on it.
/// </summary>
public sealed partial record Surface : Component, IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout
{
    /// <summary>What the surface contains.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    /// <summary>Whether children are cut to the surface's rounded shape.</summary>
    public bool ClipContent { get; init; }

    /// <summary>What assistive technology should know about the surface.</summary>
    public Semantics? Semantics { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var state = context.UseSurface().With(this.ToSurfaceChange());
        return ThemeContexts.Surface.Provide(state, SurfaceBox.For(this, theme, state) with
        {
            ClipContent = ClipContent,
            Semantics = Semantics,
            Children = Children,
        });
    }
}
