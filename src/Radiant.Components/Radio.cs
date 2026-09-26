using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A radio button: one choice of several, controlled by its owner. Pressing it calls
/// <see cref="OnSelect"/>; the owner then marks it <see cref="Selected"/> and the others not.
/// </summary>
/// <param name="Selected">Whether it's the chosen one.</param>
/// <param name="OnSelect">Called when pressed.</param>
[RequiresTestId]
public sealed record Radio(bool Selected, Action? OnSelect) : Component
{
    /// <summary>A label beside it, which can also be pressed.</summary>
    public string? Label { get; init; }

    /// <summary>What assistive technology calls it when there's no visible <see cref="Label"/> (a check box in a table row).</summary>
    public string? AccessibleLabel { get; init; }

    /// <summary>Whether it can't be chosen.</summary>
    public bool Disabled { get; init; }

    /// <summary>Tab order: 0 in tree order, negative to leave it out of tabbing (a group's other options).</summary>
    public int TabIndex { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var motion = theme.Theme.Motion;
        var dot = context.UseTransition(Selected ? 1f : 0f, motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);
        var style = theme.Theme.Components.Selection;
        var recolor = Disabled && theme.RecolorsDisabled();
        var outer = style.RadioSize;
        var select = OnSelect;

        Element Circle(SelectionVisualState state)
        {
            if (style.Halo)
            {
                // A ring in the accent (the quiet content colour when not chosen) with a dot growing in it.
                var colour = recolor
                    ? theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } })
                    : Selected ? theme.Get(SurfaceName.Primary) : theme.Get(SurfaceName.SurfaceVariant, on: true);
                var size = outer / 2f * dot;
                return new Box
                {
                    Layout = new LayoutStyle { Width = outer, Height = outer, AlignItems = Align.Center, JustifyContent = Justify.Center },
                    BorderWidth = style.BorderWidth,
                    BorderColor = colour,
                    CornerRadii = Radiant.Graphics2D.CornerRadii.All(outer / 2f),
                    Children =
                    [
                        size <= 0.5f ? null : new Box
                        {
                            Layout = new LayoutStyle { Width = size, Height = size },
                            Background = colour,
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(size / 2f),
                        },
                    ],
                };
            }
            // Flat: an outlined disc that fills with the accent when chosen, a small dot in its "on" colour.
            var outline = recolor
                ? theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } })
                : state.Hovered && !state.Disabled ? theme.Get(SurfaceName.SurfaceVariant, on: true) : theme.Outline;
            var accent = recolor ? outline : theme.Get(SurfaceName.Primary);
            var blank = theme.Get(SurfaceName.SurfaceBright);
            var inner = outer * 0.375f * dot;
            return new Box
            {
                Layout = new LayoutStyle { Width = outer, Height = outer, AlignItems = Align.Center, JustifyContent = Justify.Center },
                BorderWidth = dot >= 1f ? 0f : style.BorderWidth,
                BorderColor = outline,
                Background = Radiant.Graphics2D.Color.Lerp(blank, accent, dot),
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(outer / 2f),
                Children =
                [
                    inner <= 0.5f ? null : new Box
                    {
                        Layout = new LayoutStyle { Width = inner, Height = inner },
                        Background = recolor ? theme.SurfaceColor(surface) : theme.Get(SurfaceName.Primary, on: true),
                        CornerRadii = Radiant.Graphics2D.CornerRadii.All(inner / 2f),
                    },
                ],
            };
        }
        return new SelectionControl(Circle, outer)
        {
            IndicatorHeight = outer,
            IndicatorRadius = outer / 2f,
            Label = Label,
            AccessibleLabel = AccessibleLabel,
            Role = SemanticsRole.RadioButton,
            Checked = Selected,
            Disabled = Disabled,
            TabIndex = TabIndex,
            OnToggle = () =>
            {
                if (!Selected)
                {
                    select?.Invoke();
                }
            },
        };
    }
}
