using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A check box, controlled by its owner: it shows <see cref="Checked"/> and reports presses through
/// <see cref="OnChange"/> with the value it should become.
/// <code>new Checkbox(agreed.Value, agreed.Set) { Label = "I agree" }</code>
/// </summary>
/// <param name="Checked">Whether it's ticked.</param>
/// <param name="OnChange">Called with the new value when pressed.</param>
[RequiresTestId]
public sealed record Checkbox(bool Checked, Action<bool>? OnChange) : Component
{
    /// <summary>A label beside it, which can also be pressed.</summary>
    public string? Label { get; init; }

    /// <summary>What assistive technology calls it when there's no visible <see cref="Label"/> (a check box in a table row).</summary>
    public string? AccessibleLabel { get; init; }

    /// <summary>Whether to show the mixed state (a dash) instead: some but not all of a group ticked.</summary>
    public bool Indeterminate { get; init; }

    /// <summary>Whether it can't be changed.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether to show the error colour.</summary>
    public bool Error { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var on = Checked || Indeterminate;
        var family = Error ? SurfaceName.Error : SurfaceName.Primary;
        var motion = theme.Theme.Motion;
        var fill = context.UseTransition(on ? 1f : 0f, motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);

        var style = theme.Theme.Components.Selection;
        var recolor = Disabled && theme.RecolorsDisabled();
        var size = style.CheckboxSize;

        // Unticked: an outline in the quiet content colour (or, without halos, the outline colour,
        // darkening under the pointer). Ticked: filled in the accent, with its "on" colour check.
        Element Box(SelectionVisualState state)
        {
            var outline = recolor
                ? theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } })
                : Error ? theme.Get(SurfaceName.Error)
                : style.Halo ? theme.Get(SurfaceName.SurfaceVariant, on: true)
                : state.Hovered && !state.Disabled ? theme.Get(SurfaceName.SurfaceVariant, on: true) : theme.Outline;
            var accent = recolor ? outline : theme.Get(family);
            var mark = recolor ? theme.SurfaceColor(surface) : theme.Get(family, on: true);
            return new Box
            {
                Layout = new LayoutStyle { Width = size, Height = size, AlignItems = Align.Center, JustifyContent = Justify.Center },
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(style.CheckboxRadius),
                BorderWidth = fill >= 1f ? 0f : style.BorderWidth,
                BorderColor = outline,
                Background = fill <= 0f ? (style.Halo ? null : theme.Get(SurfaceName.SurfaceBright))
                    : Radiant.Graphics2D.Color.Lerp(style.Halo ? Radiant.Graphics2D.Color.Transparent : theme.Get(SurfaceName.SurfaceBright), accent, fill),
                Children =
                [
                    fill <= 0.5f ? null : new TextBlock(Indeterminate ? "remove" : "check")
                    {
                        IsDecorative = true,
                        Wrap = false,
                        Style = new Radiant.Text.TextStyle
                        {
                            FontFamily = Radiant.Text.FontLibrary.Icons,
                            Size = size,
                            LineHeight = size,
                            Weight = 700,
                            Color = mark,
                        },
                        Layout = new LayoutStyle { Width = size, Height = size },
                    },
                ],
            };
        }
        var onChange = OnChange;
        var next = !Checked;
        return new SelectionControl(Box, size)
        {
            IndicatorHeight = size,
            IndicatorRadius = style.CheckboxRadius,
            Label = Label,
            AccessibleLabel = AccessibleLabel,
            Role = SemanticsRole.CheckBox,
            Checked = Indeterminate ? null : Checked,
            Disabled = Disabled,
            OnToggle = () => onChange?.Invoke(next),
        };
    }
}
