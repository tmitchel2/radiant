using System;
using Radiant.Layout;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Keeps its content at a width-to-height ratio (16/9 for a video, 1 for a square thumbnail) as its
/// width changes: it takes the width it's given and sets its height from it.
/// </summary>
/// <param name="Ratio">Width divided by height.</param>
/// <param name="Child">The content, filling the box.</param>
public sealed record AspectRatio(float Ratio, Element? Child) : Component
{
    /// <summary>The box's own layout, added to its default (stretching across its parent).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Ratio);
        return new Box
        {
            ClipContent = true,
            Layout = new LayoutStyle { AlignSelf = Align.Stretch, AspectRatio = Ratio }.Merge(Layout ?? default),
            Children = [Child],
        };
    }
}
