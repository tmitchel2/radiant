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
public sealed record Checkbox(bool Checked, Action<bool>? OnChange) : Component
{
    /// <summary>A label beside it, which can also be pressed.</summary>
    public string? Label { get; init; }

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

        // Unticked: an outline in the quiet content colour. Ticked: filled in the accent, with its "on" colour check.
        var outline = Disabled
            ? theme.ContentColor(surface with { Content = surface.Content with { Opacity = Legibility.Low } })
            : Error ? theme.Get(SurfaceName.Error) : theme.Get(SurfaceName.SurfaceVariant, on: true);
        var accent = Disabled ? outline : theme.Get(family);
        var mark = Disabled ? theme.SurfaceColor(surface) : theme.Get(family, on: true);
        var box = new Box
        {
            Layout = new LayoutStyle { Width = 18, Height = 18, AlignItems = Align.Center, JustifyContent = Justify.Center },
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(2),
            BorderWidth = fill >= 1f ? 0f : 2f,
            BorderColor = outline,
            Background = fill <= 0f ? null : Radiant.Graphics2D.Color.Lerp(Radiant.Graphics2D.Color.Transparent, accent, fill),
            Children =
            [
                fill <= 0.5f ? null : new TextBlock(Indeterminate ? "remove" : "check")
                {
                    IsDecorative = true,
                    Wrap = false,
                    Style = new Radiant.Text.TextStyle
                    {
                        FontFamily = Radiant.Text.FontLibrary.Icons,
                        Size = 18,
                        LineHeight = 18,
                        Weight = 700,
                        Color = mark,
                    },
                    Layout = new LayoutStyle { Width = 18, Height = 18 },
                },
            ],
        };
        var onChange = OnChange;
        var next = !Checked;
        return new SelectionControl(_ => box, 18)
        {
            Label = Label,
            Role = SemanticsRole.CheckBox,
            Checked = Indeterminate ? null : Checked,
            Disabled = Disabled,
            OnToggle = () => onChange?.Invoke(next),
        };
    }
}
