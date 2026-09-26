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

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var motion = theme.Theme.Motion;
        var dot = context.UseTransition(Selected ? 1f : 0f, motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);
        var colour = Disabled
            ? theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } })
            : Selected ? theme.Get(SurfaceName.Primary) : theme.Get(SurfaceName.SurfaceVariant, on: true);
        var size = 10f * dot;
        var select = OnSelect;
        var circle = new Box
        {
            Layout = new LayoutStyle { Width = 20, Height = 20, AlignItems = Align.Center, JustifyContent = Justify.Center },
            BorderWidth = 2f,
            BorderColor = colour,
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(10),
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
        return new SelectionControl(_ => circle, 20)
        {
            Label = Label,
            AccessibleLabel = AccessibleLabel,
            Role = SemanticsRole.RadioButton,
            Checked = Selected,
            Disabled = Disabled,
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
