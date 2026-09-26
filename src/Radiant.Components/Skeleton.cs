using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A placeholder shaped like content that is still loading, gently pulsing. Give it the size and
/// corners of what will replace it.
/// </summary>
public sealed record Skeleton : Component
{
    /// <summary>Its size and placement.</summary>
    public LayoutStyle Layout { get; init; } = new() { Height = 16, AlignSelf = Align.Stretch };

    /// <summary>Its corners.</summary>
    public CornerShapeRole CornerShape { get; init; } = CornerShapeRole.ExtraSmall;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var time = context.UseState(0.0);
        var root = context.Root;
        var reduced = theme.Theme.Motion.Reduced;
        context.UseEffect(() => reduced ? null : root.AddTicker(seconds => time.Update(t => t + seconds), TickerKind.Continuous, "skeleton pulse").Dispose, reduced);
        var pulse = reduced ? 1f : 0.7f + 0.3f * MathF.Cos((float)(time.Value * Math.Tau / 1.6));
        var surface = context.UseSurface();
        return new Box
        {
            Layout = Layout,
            Background = theme.StateLayerColor(surface, 0.08f * pulse + 0.04f),
            CornerRadii = theme.Corners(CornerShape),
        };
    }
}
