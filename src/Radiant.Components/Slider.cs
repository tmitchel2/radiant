using System;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Picks a value along a range by dragging the handle, pressing the track, or with the keyboard
/// (arrows step, Page Up and Down take ten steps, Home and End go to the ends). Controlled by its
/// owner through <see cref="Value"/> and <see cref="OnChange"/>.
/// </summary>
/// <param name="Value">The value.</param>
/// <param name="OnChange">Called with the new value while it's changed.</param>
[RequiresTestId]
public sealed record Slider(float Value, Action<float>? OnChange) : Component
{
    /// <summary>The least value.</summary>
    public float Min { get; init; }

    /// <summary>The greatest value.</summary>
    public float Max { get; init; } = 1f;

    /// <summary>The step values snap to; null for any value (keys then move a hundredth of the range).</summary>
    public float? Step { get; init; }

    /// <summary>Whether it can't be changed.</summary>
    public bool Disabled { get; init; }

    /// <summary>What assistive technology calls it.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    /// <summary>
    /// One step on from <paramref name="value"/> in <paramref name="direction"/>. With snapping steps, a
    /// value between two goes to the next one that way, not a whole step past it.
    /// </summary>
    internal static float Toward(float value, float min, float step, bool snaps, int direction)
    {
        if (!snaps)
        {
            return value + step * direction;
        }
        var position = (value - min) / step;
        var nearest = MathF.Round(position);
        var target = MathF.Abs(position - nearest) < 1e-4f
            ? nearest + direction
            : direction > 0 ? MathF.Ceiling(position) : MathF.Floor(position);
        return min + target * step;
    }

    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var track = context.UseRef(new ElementRef()).Value;
        var line = context.UseRef(new ElementRef()).Value;
        var dragging = context.UseRef(false);
        var hovered = context.UseState(false);
        var focusRing = context.UseState(false);
        var pressed = context.UseState(false);
        var latest = context.UseRef(this);
        latest.Value = this;

        var span = MathF.Max(Max - Min, 1e-6f);
        var fraction = Math.Clamp((Value - Min) / span, 0f, 1f);
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Low } };
        var style = theme.Theme.Components.Selection;
        var interaction = theme.Theme.Components.Interaction;
        var recolor = Disabled && theme.RecolorsDisabled();
        var thickness = style.TrackThickness;
        var active = recolor ? theme.ContentColor(faded) : theme.Get(SurfaceName.Primary);
        var inactive = recolor ? theme.StateLayerColor(surface, 0.12f)
            : theme.Track();
        var layers = theme.Theme.StateLayers;
        var layer = Disabled ? 0f : pressed.Value ? layers.Pressed : focusRing.Value ? layers.Focus : hovered.Value ? layers.Hover : 0f;

        float Snap(float value)
        {
            var props = latest.Value;
            value = Math.Clamp(value, props.Min, props.Max);
            return props.Step is { } step && step > 0 ? props.Min + MathF.Round((value - props.Min) / step) * step : value;
        }

        void Set(float value)
        {
            var props = latest.Value;
            var next = Snap(value);
            if (!props.Disabled && next != props.Value)
            {
                props.OnChange?.Invoke(next);
            }
        }

        void SetFromPointer(PointerEventArgs e)
        {
            var bounds = line.Bounds;
            var props = latest.Value;
            if (bounds.Width > 0)
            {
                // Measured from the start: the right, reading right to left.
                var along = rightToLeft ? bounds.X + bounds.Width - e.Position.X : e.Position.X - bounds.X;
                Set(props.Min + along / bounds.Width * (props.Max - props.Min));
            }
        }

        return new Box
        {
            Focusable = !Disabled,
            Ref = track,
            Semantics = new Semantics
            {
                Role = SemanticsRole.Slider,
                Label = Label,
                Value = Value.ToString("0.##", CultureInfo.InvariantCulture),
                Disabled = Disabled,
            },
            // Inset by the handle's radius, so the handle stays inside the slider at either end.
            Opacity = Disabled && !recolor ? interaction.DisabledOpacity : 1f,
            Layout = new LayoutStyle { Height = 44, MinWidth = 120, AlignSelf = Align.Stretch, JustifyContent = Justify.Center, Padding = Edges.Symmetric(10, 0) },
            OnPointerEnter = _ => hovered.Set(true),
            OnPointerLeave = _ => hovered.Set(false),
            OnPointerDown = e =>
            {
                if (latest.Value.Disabled || e.Button != PointerButton.Left)
                {
                    return;
                }
                dragging.Value = true;
                pressed.Set(true);
                SetFromPointer(e);
            },
            OnPointerMove = e =>
            {
                if (dragging.Value)
                {
                    SetFromPointer(e);
                }
            },
            OnPointerUp = _ =>
            {
                dragging.Value = false;
                pressed.Set(false);
            },
            OnKeyDown = e =>
            {
                var props = latest.Value;
                var step = props.Step ?? (props.Max - props.Min) / 100f;
                var handled = true;
                switch (e.Key.ForDirection(rightToLeft))
                {
                    case KeyCode.Right or KeyCode.Up: Set(Toward(props.Value, props.Min, step, props.Step is not null, 1)); break;
                    case KeyCode.Left or KeyCode.Down: Set(Toward(props.Value, props.Min, step, props.Step is not null, -1)); break;
                    case KeyCode.PageUp: Set(Toward(props.Value, props.Min, step, props.Step is not null, 1) + step * 9f); break;
                    case KeyCode.PageDown: Set(Toward(props.Value, props.Min, step, props.Step is not null, -1) - step * 9f); break;
                    case KeyCode.Home: Set(props.Min); break;
                    case KeyCode.End: Set(props.Max); break;
                    default: handled = false; break;
                }
                e.Handled |= handled;
            },
            OnFocus = e => focusRing.Set(e.IsFocusVisible),
            OnBlur = _ => focusRing.Set(false),
            Children =
            [
                // The inactive track, the active part up to the handle, and the handle.
                new Box
                {
                    Ref = line,
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Height = thickness },
                    Background = inactive,
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(thickness / 2f),
                    Children =
                    [
                        new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle { Width = Dimension.Percent(fraction * 100f), Height = thickness },
                            Background = active,
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(thickness / 2f),
                        },
                        new Box
                        {
                            HitTestVisible = false,
                            Layout = new LayoutStyle
                            {
                                Position = PositionType.Absolute,
                                Width = 40,
                                Height = 40,
                                Inset = new Edges(Dimension.Percent(fraction * 100f), -(40f - thickness) / 2f, Dimension.Undefined, Dimension.Undefined),
                                Margin = new Edges(-20, 0, 0, 0),
                                AlignItems = Align.Center,
                                JustifyContent = Justify.Center,
                            },
                            Background = style.Halo && layer > 0f ? theme.StateLayerColor(surface with { Content = new SurfaceRoleState(SurfaceName.Primary, false, false) }, layer) : null,
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(20),
                            Children =
                            [
                                .. SliderParts.Thumb(theme, active, focusRing.Value && !Disabled, raised: !recolor),
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
