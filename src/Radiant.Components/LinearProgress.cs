using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A bar showing how far something has got: <see cref="Value"/> from 0 to 1, or, without one, a
/// segment that keeps sliding across (indeterminate: busy, but no idea how long).
/// </summary>
public sealed record LinearProgress : Component
{
    /// <summary>How far, 0 to 1; null for indeterminate.</summary>
    public float? Value { get; init; }

    /// <summary>What assistive technology calls it.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var phase = context.UseState(0.0);
        var root = context.Root;
        var indeterminate = Value is null;

        // The indeterminate segment is driven by the root's frames while shown.
        context.UseEffect(() => indeterminate && !theme.Theme.Motion.Reduced
            ? root.AddTicker(seconds => phase.Update(p => (p + seconds / 1.8) % 1.0)).Dispose
            : null, indeterminate);

        var indicator = theme.Get(SurfaceName.Primary);
        Element bar;
        if (Value is { } value)
        {
            bar = new Box
            {
                Layout = new LayoutStyle { Width = Dimension.Percent(Math.Clamp(value, 0f, 1f) * 100f), Height = 4 },
                Background = indicator,
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(2),
            };
        }
        else
        {
            // A segment 40% wide sliding from off the left to off the right.
            var start = (float)phase.Value * 1.4f - 0.4f;
            bar = new Box
            {
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Width = Dimension.Percent(40),
                    Height = 4,
                    Inset = new Edges(Dimension.Percent(start * 100f), 0, Dimension.Undefined, Dimension.Undefined),
                },
                Background = indicator,
                CornerRadii = Radiant.Graphics2D.CornerRadii.All(2),
            };
        }
        return new Box
        {
            Semantics = new Semantics
            {
                Role = SemanticsRole.ProgressIndicator,
                Label = Label,
                Value = Value is { } v ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{MathF.Round(v * 100)}%") : null,
            },
            Layout = new LayoutStyle { Height = 4, AlignSelf = Align.Stretch },
            Background = theme.Get(SurfaceName.Secondary, container: true),
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(2),
            ClipContent = true,
            Children = [bar],
        };
    }
}
