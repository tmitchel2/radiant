using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Gives everything below it a <see cref="Snackbars"/> and shows its messages at the bottom
/// centre of the window, each for its duration, sliding in and out. Put one near the root.
/// </summary>
/// <param name="Child">The app.</param>
public sealed record SnackbarHost(Element? Child) : Component
{
    /// <summary>The context the queue is provided in.</summary>
    public static Context<Snackbars?> Queue { get; } = new(null);

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var queue = context.UseRef(new Snackbars()).Value;
        var current = context.Watch(queue.Current);
        var shown = context.UseRef<SnackbarMessage?>(null);
        if (current is not null)
        {
            shown.Value = current;
        }
        var root = context.Root;

        // Each message times out on the root's frames.
        context.UseEffect(() =>
        {
            if (current is null)
            {
                return null;
            }
            var remaining = (current.Duration ?? TimeSpan.FromSeconds(current.ActionLabel is null ? 4 : 8)).TotalSeconds;
            IDisposable? ticker = null;
            ticker = root.AddTicker(seconds =>
            {
                remaining -= seconds;
                if (remaining <= 0)
                {
                    ticker?.Dispose();
                    queue.Dismiss();
                }
            }, TickerKind.Timer, "snackbar timeout");
            return ticker.Dispose;
        }, current);

        var message = shown.Value;
        return new Fragment(
            Queue.Provide(queue, Child),
            new Presence(current is not null, progress => message is null ? null : new Portal(new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Inset = new Edges(0, Dimension.Undefined, 0, 16f - 24f * (1f - progress)),
                    AlignItems = Align.Center,
                },
                Children =
                [
                    new Surface
                    {
                        SurfaceColor = SurfaceName.Inverse,
                        CornerShape = CornerShapeRole.ExtraSmall,
                        Elevation = ElevationLevel.Level3,
                        Semantics = new Semantics { Role = SemanticsRole.Alert, Label = message.Text },
                        Layout = new LayoutStyle
                        {
                            FlexDirection = FlexDirection.Row,
                            AlignItems = Align.Center,
                            MinHeight = 48,
                            MinWidth = 288,
                            MaxWidth = 560,
                            Padding = new Edges(16, 4, message.ActionLabel is null ? 16 : 8, 4),
                            ColumnGap = 8,
                        },
                        Children =
                        [
                            new Box
                            {
                                Opacity = progress,
                                Layout = new LayoutStyle { FlexGrow = 1, FlexShrink = 1 },
                                Children = [new SurfaceText(message.Text) { TextType = TextType.BodyMedium }],
                            },
                            message.ActionLabel is null ? null : new SurfaceButton(message.ActionLabel, ButtonVariant.Text)
                            {
                                // Material's inverse primary: the inverse role's container colour itself,
                                // not the colour on it (which is the snackbar's own).
                                ContentColor = SurfaceName.Inverse,
                                ContentOnToggle = true,
                                ContentContainerToggle = true,
                                OnPress = () =>
                                {
                                    message.OnAction?.Invoke();
                                    queue.Dismiss();
                                },
                            },
                        ],
                    },
                ],
            })) { Duration = TimeSpan.FromMilliseconds(150) });
    }
}
