using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Platform;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Two panes with a divider between them that can be dragged to resize them. One pane
/// (<see cref="SizedPane"/>) has a set size and the other takes the rest; when the splitter is
/// too small for both, the sized pane gives way down to <see cref="MinSize"/>. The divider can
/// also be moved with the keyboard (arrows step, Shift for larger steps, Home and End go to the
/// limits), and a double click puts it back to <see cref="InitialSize"/>. The size is kept by the
/// splitter unless <see cref="Size"/> is set, when its owner keeps it through
/// <see cref="OnSizeChange"/>.
/// </summary>
/// <param name="First">The left (or top) pane.</param>
/// <param name="Second">The right (or bottom) pane.</param>
public sealed record Splitter(Element? First, Element? Second) : Component
{
    private const float Step = 16f;
    private const float LargeStep = 64f;
    private const float Reach = 4f;

    /// <summary>Side by side (the default) or stacked.</summary>
    public Orientation Orientation { get; init; }

    /// <summary>Which pane has the set size.</summary>
    public SplitterPane SizedPane { get; init; }

    /// <summary>The sized pane's size at first, and after a double click on the divider.</summary>
    public float InitialSize { get; init; } = 280f;

    /// <summary>The sized pane's size, when the owner keeps it; null for the splitter to keep it.</summary>
    public float? Size { get; init; }

    /// <summary>Called with the new size as the divider moves.</summary>
    public Action<float>? OnSizeChange { get; init; }

    /// <summary>The least the sized pane can be.</summary>
    public float MinSize { get; init; } = 120f;

    /// <summary>The most the sized pane can be.</summary>
    public float MaxSize { get; init; } = float.PositiveInfinity;

    /// <summary>The least the other pane can be; dragging stops there.</summary>
    public float MinOtherSize { get; init; } = 120f;

    /// <summary>What assistive technology calls the divider.</summary>
    public string? Label { get; init; }

    /// <summary>The splitter's own layout, added to its default (growing to fill its parent).</summary>
    public LayoutStyle? Layout { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var own = context.UseState(InitialSize);
        var container = context.UseRef(new ElementRef()).Value;
        var sized = context.UseRef(new ElementRef()).Value;
        var drag = context.UseRef<(float Pointer, float Size)?>(null);
        var hovered = context.UseState(false);
        var focusRing = context.UseState(false);
        var dragging = context.UseState(false);
        var latest = context.UseRef(this);
        latest.Value = this;

        var horizontal = Orientation == Orientation.Horizontal;
        var sizedFirst = SizedPane == SplitterPane.First;
        var size = Size ?? own.Value;

        // Measured towards the end: leftwards, reading right to left.
        float Along(System.Numerics.Vector2 point) => horizontal ? (rightToLeft ? -point.X : point.X) : point.Y;

        float Extent(System.Drawing.RectangleF bounds) => horizontal ? bounds.Width : bounds.Height;

        // The largest the sized pane can be: its maximum, less what the other pane needs.
        float Largest()
        {
            var props = latest.Value;
            var room = container.IsMounted ? Extent(container.Bounds) - 1f - props.MinOtherSize : float.PositiveInfinity;
            return MathF.Max(props.MinSize, MathF.Min(props.MaxSize, room));
        }

        void Set(float value)
        {
            var props = latest.Value;
            var next = Math.Clamp(value, props.MinSize, Largest());
            if (props.Size is null)
            {
                own.Set(next);
            }
            if (next != (props.Size ?? own.Value))
            {
                props.OnSizeChange?.Invoke(next);
            }
        }

        // Moving the divider towards the end grows the first pane, whichever pane is sized.
        void Move(float delta, float from) => Set(from + (sizedFirst ? delta : -delta));

        // The shown size, which may be less than the set one when the splitter is squeezed.
        float Shown() => sized.IsMounted ? Extent(sized.Bounds) : latest.Value.Size ?? own.Value;

        var paneSize = new LayoutStyle
        {
            Width = horizontal ? size : Dimension.Undefined,
            Height = horizontal ? Dimension.Undefined : size,
            MinWidth = horizontal ? MinSize : Dimension.Undefined,
            MinHeight = horizontal ? Dimension.Undefined : MinSize,
            FlexShrink = 1,
        };
        var paneRest = new LayoutStyle
        {
            FlexGrow = 1,
            FlexShrink = 1,
            FlexBasis = 0,
            MinWidth = horizontal ? MinOtherSize : Dimension.Undefined,
            MinHeight = horizontal ? Dimension.Undefined : MinOtherSize,
        };
        var active = dragging.Value || focusRing.Value || hovered.Value;

        // The handle sits inside the second pane's wrapper, reaching back over the line and into
        // the first pane: later in the tree, so it's above both panes where it overlaps them.
        var handle = new Box
        {
            Focusable = true,
            Cursor = horizontal ? CursorShape.ResizeLeftRight : CursorShape.ResizeUpDown,
            Semantics = new Semantics
            {
                Role = SemanticsRole.Separator,
                Label = Label,
                Value = size.ToString("0", CultureInfo.InvariantCulture),
            },
            Layout = new LayoutStyle
            {
                Position = PositionType.Absolute,
                Width = horizontal ? Reach * 2f + 1f : Dimension.Percent(100),
                Height = horizontal ? Dimension.Percent(100) : Reach * 2f + 1f,
                Inset = horizontal ? new Edges(-(Reach + 1f), 0, Dimension.Undefined, Dimension.Undefined) : new Edges(0, -(Reach + 1f), Dimension.Undefined, Dimension.Undefined),
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
            },
            OnPointerEnter = _ => hovered.Set(true),
            OnPointerLeave = _ => hovered.Set(false),
            OnPointerDown = e =>
            {
                if (e.Button != PointerButton.Left)
                {
                    return;
                }
                drag.Value = (Along(e.Position), Shown());
                dragging.Set(true);
                e.Handled = true;
            },
            OnPointerMove = e =>
            {
                if (drag.Value is { } start)
                {
                    Move(Along(e.Position) - start.Pointer, start.Size);
                }
            },
            OnPointerUp = _ =>
            {
                drag.Value = null;
                dragging.Set(false);
            },
            OnClick = e =>
            {
                if (e.ClickCount == 2)
                {
                    Set(latest.Value.InitialSize);
                    e.Handled = true;
                }
            },
            OnKeyDown = e =>
            {
                var step = (e.Modifiers & KeyModifiers.Shift) != 0 ? LargeStep : Step;
                var (back, forward) = horizontal ? (KeyCode.Left.ForDirection(rightToLeft), KeyCode.Right.ForDirection(rightToLeft)) : (KeyCode.Up, KeyCode.Down);
                var handled = true;
                if (e.Key == back)
                {
                    Move(-step, Shown());
                }
                else if (e.Key == forward)
                {
                    Move(step, Shown());
                }
                else if (e.Key == KeyCode.Home)
                {
                    Set(latest.Value.MinSize);
                }
                else if (e.Key == KeyCode.End)
                {
                    Set(Largest());
                }
                else
                {
                    handled = false;
                }
                e.Handled |= handled;
            },
            OnFocus = e => focusRing.Set(e.IsFocusVisible),
            OnBlur = _ => focusRing.Set(false),
            Children =
            [
                // A thicker line in the primary colour while the divider is hovered, held or focused.
                active ? new Box
                {
                    HitTestVisible = false,
                    Layout = horizontal
                        ? new LayoutStyle { Width = 3, AlignSelf = Align.Stretch }
                        : new LayoutStyle { Height = 3, AlignSelf = Align.Stretch },
                    Background = theme.Get(SurfaceName.Primary),
                } : null,
            ],
        };

        // Panes clip what they hold. The second's clip is a box inside it, so the handle, which
        // reaches out of the second pane, isn't clipped.
        var first = new Box
        {
            Ref = sizedFirst ? sized : null,
            ClipContent = true,
            Layout = sizedFirst ? paneSize : paneRest,
            Children = [First],
        };
        var second = new Box
        {
            Ref = sizedFirst ? null : sized,
            Layout = sizedFirst ? paneRest : paneSize,
            Children = [new Box { ClipContent = true, Layout = new LayoutStyle { FlexGrow = 1 }, Children = [Second] }, handle],
        };
        var line = new Box
        {
            HitTestVisible = false,
            Layout = horizontal ? new LayoutStyle { Width = 1, FlexShrink = 0 } : new LayoutStyle { Height = 1, FlexShrink = 0 },
            Background = theme.OutlineVariant,
        };
        return new Box
        {
            Ref = container,
            Layout = new LayoutStyle
            {
                FlexDirection = horizontal ? FlexDirection.Row : FlexDirection.Column,
                FlexGrow = 1,
                FlexShrink = 1,
                AlignItems = Align.Stretch,
            }.Merge(Layout ?? default),
            Children = [first, line, second],
        };
    }
}
