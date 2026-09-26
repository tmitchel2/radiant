using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A container for related content: a <see cref="Surface"/> with medium corners and a container
/// colour, raised, filled or outlined by its variant. Facets set on the card override the variant.
/// </summary>
[ForwardFacets(typeof(Surface), "Surface",
    typeof(IHasBackgroundColor), typeof(IHasCornerShape), typeof(IHasElevation), typeof(IHasOutline), typeof(IHasLayout))]
public sealed partial record Card : Component, IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout
{
    /// <summary>A card holding <paramref name="children"/>.</summary>
    public Card(params Element?[] children) => Children = children;

    /// <summary>How the card stands out.</summary>
    public CardVariant Variant { get; init; }

    /// <summary>What the card contains.</summary>
    public IReadOnlyList<Element?> Children { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        var style = context.UseTheme().Theme.Components.Card;
        var look = Variant switch
        {
            CardVariant.Filled => style.Filled,
            CardVariant.Outlined => style.Outlined,
            _ => style.Elevated,
        };
        return ForwardSurface(SurfaceLooks.Surface(look) with
        {
            CornerShape = style.Shape,
            Layout = new LayoutStyle { Padding = Edges.All(style.Padding), RowGap = 8 },
        }) with
        {
            ClipContent = true,
            Children = Children,
        };
    }
}
