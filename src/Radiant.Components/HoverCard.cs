using System;
using Radiant.Components.Primitives;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A card of rich content (a person's profile, a link's preview) that appears beside its trigger
/// once the pointer rests on it, and stays while the pointer moves onto the card, so its content
/// can be read and pressed. It goes a moment after the pointer leaves both. For pointer users
/// only: what it shows should also be reachable another way.
/// </summary>
/// <param name="Trigger">What the pointer rests on.</param>
/// <param name="Content">The card's content.</param>
public sealed record HoverCard(Element? Trigger, Element? Content) : Component
{
    /// <summary>How long the pointer must rest on the trigger.</summary>
    public TimeSpan OpenDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>How long after the pointer leaves the card goes.</summary>
    public TimeSpan CloseDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Which side of the trigger it shows on.</summary>
    public Side Side { get; init; } = Side.Bottom;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var anchor = context.UseRef(new ElementRef()).Value;
        var overTrigger = context.UseState(false);
        var overCard = context.UseState(false);
        var visible = context.UseState(false);
        var root = context.Root;
        var over = overTrigger.Value || overCard.Value;
        var shown = visible.Value;
        var (openDelay, closeDelay) = (OpenDelay.TotalSeconds, CloseDelay.TotalSeconds);

        // Rest to open, leave to close: counted on the root's frames while it's due to change.
        context.UseEffect(() =>
        {
            if (over == shown)
            {
                return null;
            }
            var waited = 0.0;
            var wait = over ? openDelay : closeDelay;
            IDisposable? ticker = null;
            ticker = root.AddTicker(seconds =>
            {
                waited += seconds;
                if (waited >= wait)
                {
                    visible.Set(over);
                    ticker?.Dispose();
                }
            });
            return ticker.Dispose;
        }, (over, shown));

        var (content, side) = (Content, Side);
        return new Fragment(
            new Box
            {
                Ref = anchor,
                OnPointerEnter = _ => overTrigger.Set(true),
                OnPointerLeave = _ => overTrigger.Set(false),
                Children = [Trigger],
            },
            new Presence(shown, progress => new Anchored(anchor, new Box
            {
                OnPointerEnter = _ => overCard.Set(true),
                OnPointerLeave = _ => overCard.Set(false),
                Children =
                [
                    new Surface
                    {
                        SurfaceColor = SurfaceName.SurfaceContainerHigh,
                        CornerShape = CornerShapeRole.Medium,
                        Elevation = ElevationLevel.Level2,
                        Semantics = new Semantics { Role = SemanticsRole.Dialog },
                        Layout = new LayoutStyle { Padding = Edges.All(16), MaxWidth = 320 },
                        Children = [new Box { Opacity = progress, Children = [content] }],
                    },
                ],
            }) { Side = side, Align = SideAlign.Start, Offset = 6f }) { Duration = TimeSpan.FromMilliseconds(120) });
    }
}
