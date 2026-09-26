using System;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A short label that appears over its child after the pointer rests on it (600 ms by default),
/// and goes when the pointer leaves or presses.
/// </summary>
/// <param name="Text">The tip.</param>
/// <param name="Child">What it describes.</param>
public sealed record Tooltip(string Text, Element? Child) : Component
{
    /// <summary>How long the pointer must rest before the tip shows.</summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(600);

    /// <summary>Which side of the child it shows on.</summary>
    public Side Side { get; init; } = Side.Top;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var anchor = context.UseRef(new ElementRef()).Value;
        var hovered = context.UseState(false);
        var visible = context.UseState(false);
        var root = context.Root;
        var delay = Delay.TotalSeconds;
        var isHovered = hovered.Value;

        // While hovered and not yet showing, count the rest time on the root's frames.
        context.UseEffect(() =>
        {
            if (!isHovered || visible.Value)
            {
                return null;
            }
            var waited = 0.0;
            IDisposable? ticker = null;
            ticker = root.AddTicker(seconds =>
            {
                waited += seconds;
                if (waited >= delay)
                {
                    visible.Set(true);
                    ticker?.Dispose();
                }
            }, TickerKind.Timer, "tooltip delay");
            return ticker.Dispose;
        }, (isHovered, visible.Value));

        var text = Text;
        var side = Side;
        return new Fragment(
            new Box
            {
                Ref = anchor,
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ =>
                {
                    hovered.Set(false);
                    visible.Set(false);
                },
                OnPointerDown = _ => visible.Set(false),
                Children = [Child],
            },
            new Presence(visible.Value, progress => new Anchored(anchor, new Surface
            {
                SurfaceColor = SurfaceName.Inverse,
                CornerShape = CornerShapeRole.ExtraSmall,
                Semantics = new Semantics { Role = SemanticsRole.Tooltip },
                Layout = new LayoutStyle { Padding = Edges.Symmetric(8, 4), MaxWidth = 280 },
                Children = [new Box { Opacity = progress, Children = [new SurfaceText(text) { TextType = TextType.BodySmall }] }],
            }) { Side = side, Align = SideAlign.Center }) { Duration = TimeSpan.FromMilliseconds(100) });
    }
}
