using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// An on/off switch, controlled by its owner: it shows <see cref="On"/> and reports presses
/// through <see cref="OnChange"/>. The handle slides and grows when on, as Material's does.
/// </summary>
/// <param name="On">Whether it's on.</param>
/// <param name="OnChange">Called with the new value when pressed.</param>
[RequiresTestId]
public sealed record Switch(bool On, Action<bool>? OnChange) : Component
{
    /// <summary>A label beside it, which can also be pressed.</summary>
    public string? Label { get; init; }

    /// <summary>What assistive technology calls it when there's no visible <see cref="Label"/> (a check box in a table row).</summary>
    public string? AccessibleLabel { get; init; }

    /// <summary>Whether it can't be changed.</summary>
    public bool Disabled { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var motion = theme.Theme.Motion;
        var progress = context.UseTransition(On ? 1f : 0f, motion.Reduced ? TimeSpan.Zero : motion.MediumDuration, motion.Standard);
        var faded = surface with { Content = surface.Content with { Opacity = Legibility.Low } };

        var recolor = Disabled && theme.RecolorsDisabled();
        var trackOff = theme.Get(SurfaceName.SurfaceContainerHighest);
        var trackOn = recolor ? theme.ContentColor(faded) : theme.Get(SurfaceName.Primary);
        var outline = recolor ? theme.ContentColor(faded) : theme.Outline;
        var handleOff = recolor ? theme.ContentColor(faded) : theme.Outline;
        var handleOn = recolor ? theme.SurfaceColor(surface) : theme.Get(SurfaceName.Primary, on: true);
        var onChange = OnChange;
        var next = !On;
        var compact = theme.Theme.Components.Selection.Switch == SwitchLook.Compact;

        // Compact: a 36 × 20 track, filled either way, and a 16 px white thumb that slides from 2 to 18.
        Element Compact(SelectionVisualState state) => new Box
        {
            Layout = new LayoutStyle { Width = 36, Height = 20 },
            Background = Radiant.Graphics2D.Color.Lerp(trackOff, trackOn, progress),
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(10),
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        Width = 16,
                        Height = 16,
                        Inset = new Edges(2f + 16f * progress, 2f, Dimension.Undefined, Dimension.Undefined),
                    },
                    Background = recolor ? theme.SurfaceColor(surface) : Radiant.Graphics2D.Color.White,
                    Shadows = theme.Elevation(ElevationLevel.Level1),
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(8),
                },
            ],
        };

        Element Indicator(SelectionVisualState state)
        {
            // 16 px off, 24 px on, 28 px pressed; travelling from 8 to 28 px along the 52 px track.
            var size = state.Pressed ? 28f : 16f + 8f * progress;
            var centre = 16f + 20f * progress;
            return new Box
            {
                Layout = new LayoutStyle { Width = 52, Height = 32 },
                Background = Radiant.Graphics2D.Color.Lerp(trackOff, trackOn, progress),
                BorderWidth = 2f * (1f - progress),
                BorderColor = outline,
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(16),
                Children =
                [
                    new Box
                    {
                        Layout = new LayoutStyle
                        {
                            Position = PositionType.Absolute,
                            Width = size,
                            Height = size,
                            Inset = new Edges(centre - size / 2f, 16f - size / 2f, Dimension.Undefined, Dimension.Undefined),
                        },
                        Background = Radiant.Graphics2D.Color.Lerp(handleOff, handleOn, progress),
                        CornerRadii = Radiant.Graphics2D.CornerRadii.All(size / 2f),
                    },
                ],
            };
        }

        return new SelectionControl(compact ? Compact : Indicator, compact ? 36 : 52)
        {
            IndicatorHeight = compact ? 20 : 32,
            IndicatorRadius = compact ? 10 : 16,
            Label = Label,
            AccessibleLabel = AccessibleLabel,
            Role = SemanticsRole.Switch,
            Checked = On,
            Disabled = Disabled,
            OnToggle = () => onChange?.Invoke(next),
        };
    }
}
