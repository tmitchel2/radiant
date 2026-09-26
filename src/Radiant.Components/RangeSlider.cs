using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Picks a range along a track with two handles (a price from–to): a press moves the nearer handle
/// there and drags it, and each handle is a slider of its own for the keyboard (arrows step, Page
/// Up and Down ten steps, Home and End to its limits). The handles can't pass each other.
/// Controlled: shows <paramref name="Low"/> and <paramref name="High"/> and reports each change.
/// </summary>
/// <param name="Low">The range's start.</param>
/// <param name="High">The range's end.</param>
/// <param name="OnChange">Called with the new start and end.</param>
[RequiresTestId]
public sealed record RangeSlider(float Low, float High, Action<float, float>? OnChange) : Component
{
    /// <summary>The least value.</summary>
    public float Min { get; init; }

    /// <summary>The greatest value.</summary>
    public float Max { get; init; } = 1f;

    /// <summary>The step values snap to; null for any value (keys then move a hundredth of the range).</summary>
    public float? Step { get; init; }

    /// <summary>What assistive technology calls the range ("Price"); the handles are its minimum and maximum.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var line = context.UseRef(new ElementRef()).Value;
        var dragging = context.UseRef(-1);
        var rings = context.UseState((Low: false, High: false));
        var latest = context.UseRef(this);
        latest.Value = this;

        var span = MathF.Max(Max - Min, 1e-6f);
        var lowFraction = Math.Clamp((Low - Min) / span, 0f, 1f);
        var highFraction = Math.Clamp((High - Min) / span, 0f, 1f);
        var active = theme.Get(SurfaceName.Primary);
        var inactive = theme.Get(SurfaceName.Secondary, container: true);

        float Snap(float value)
        {
            var props = latest.Value;
            value = Math.Clamp(value, props.Min, props.Max);
            return props.Step is { } step && step > 0 ? props.Min + MathF.Round((value - props.Min) / step) * step : value;
        }

        // Moves one end, holding it at the other so the handles don't cross.
        void Set(int handle, float value)
        {
            var props = latest.Value;
            var next = Snap(value);
            var (low, high) = handle == 0 ? (MathF.Min(next, props.High), props.High) : (props.Low, MathF.Max(next, props.Low));
            if (low != props.Low || high != props.High)
            {
                props.OnChange?.Invoke(low, high);
            }
        }

        float ValueAt(PointerEventArgs e)
        {
            var bounds = line.Bounds;
            var props = latest.Value;
            var along = rightToLeft ? bounds.X + bounds.Width - e.Position.X : e.Position.X - bounds.X;
            return bounds.Width <= 0 ? props.Min : props.Min + along / bounds.Width * (props.Max - props.Min);
        }

        Element Handle(int index, float fraction, float value, bool ring) => new Box
        {
            Focusable = true,
            Semantics = new Semantics
            {
                Role = SemanticsRole.Slider,
                Label = (Label is null ? "" : Label + " ") + (index == 0 ? "minimum" : "maximum"),
                Value = value.ToString("0.##", CultureInfo.InvariantCulture),
            },
            Layout = new LayoutStyle
            {
                Position = PositionType.Absolute,
                Width = 40,
                Height = 40,
                Inset = new Edges(Dimension.Percent(fraction * 100f), -18, Dimension.Undefined, Dimension.Undefined),
                Margin = new Edges(-20, 0, 0, 0),
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
            },
            Background = ring ? theme.StateLayerColor(surface with { Content = new SurfaceRoleState(SurfaceName.Primary, false, false) }, theme.Theme.StateLayers.Focus) : null,
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(20),
            OnKeyDown = e =>
            {
                var props = latest.Value;
                var step = props.Step ?? (props.Max - props.Min) / 100f;
                var current = index == 0 ? props.Low : props.High;
                var handled = true;
                switch (e.Key.ForDirection(rightToLeft))
                {
                    case KeyCode.Right or KeyCode.Up: Set(index, current + step); break;
                    case KeyCode.Left or KeyCode.Down: Set(index, current - step); break;
                    case KeyCode.PageUp: Set(index, current + step * 10f); break;
                    case KeyCode.PageDown: Set(index, current - step * 10f); break;
                    case KeyCode.Home: Set(index, index == 0 ? props.Min : props.Low); break;
                    case KeyCode.End: Set(index, index == 0 ? props.High : props.Max); break;
                    default: handled = false; break;
                }
                e.Handled |= handled;
            },
            OnFocus = e => rings.Set(index == 0 ? rings.Value with { Low = e.IsFocusVisible } : rings.Value with { High = e.IsFocusVisible }),
            OnBlur = _ => rings.Set(index == 0 ? rings.Value with { Low = false } : rings.Value with { High = false }),
            Children =
            [
                new Box
                {
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Width = 20, Height = 20 },
                    Background = active,
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(10),
                    Shadows = theme.Elevation(ElevationLevel.Level1),
                },
            ],
        };

        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { Height = 44, MinWidth = 120, AlignSelf = Align.Stretch, JustifyContent = Justify.Center, Padding = Edges.Symmetric(10, 0) },
            OnPointerDown = e =>
            {
                if (e.Button != PointerButton.Left)
                {
                    return;
                }
                // The nearer handle takes the press; at a tie, the one the press is beyond.
                var props = latest.Value;
                var value = ValueAt(e);
                var handle = MathF.Abs(value - props.Low) < MathF.Abs(value - props.High) || value < props.Low ? 0 : 1;
                dragging.Value = handle;
                Set(handle, value);
            },
            OnPointerMove = e =>
            {
                if (dragging.Value >= 0)
                {
                    Set(dragging.Value, ValueAt(e));
                }
            },
            OnPointerUp = _ => dragging.Value = -1,
            Children =
            [
                new Box
                {
                    Ref = line,
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Height = 4 },
                    Background = inactive,
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(2),
                    Children =
                    [
                        new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle
                            {
                                Position = PositionType.Absolute,
                                Height = 4,
                                Inset = new Edges(Dimension.Percent(lowFraction * 100f), 0, Dimension.Undefined, Dimension.Undefined),
                                Width = Dimension.Percent((highFraction - lowFraction) * 100f),
                            },
                            Background = active,
                        },
                        Handle(0, lowFraction, Low, rings.Value.Low),
                        Handle(1, highFraction, High, rings.Value.High),
                    ],
                },
            ],
        };
    }
}
