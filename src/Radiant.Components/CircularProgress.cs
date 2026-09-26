using System;
using System.Globalization;
using System.Numerics;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A ring showing how far something has got (<see cref="Value"/> 0 to 1, filling clockwise from
/// the top), or, without a value, a spinner: an arc that turns while it grows and shrinks.
/// </summary>
public sealed record CircularProgress : Component
{
    /// <summary>How far, 0 to 1; null for a spinner.</summary>
    public float? Value { get; init; }

    /// <summary>The diameter.</summary>
    public float Size { get; init; } = 40f;

    /// <summary>The stroke's thickness.</summary>
    public float Thickness { get; init; } = 4f;

    /// <summary>What assistive technology calls it.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var time = context.UseState(0.0);
        var root = context.Root;
        var spinning = Value is null;
        context.UseEffect(() => spinning && !theme.Theme.Motion.Reduced
            ? root.AddTicker(seconds => time.Update(t => t + seconds), TickerKind.Continuous, "circular progress").Dispose
            : null, spinning);

        var indicator = (Vector4)theme.Get(SurfaceName.Primary);
        var track = (Vector4)theme.Track();
        var (size, thickness, value, t) = (Size, Thickness, Value, time.Value);
        return new Canvas((paint, origin, box) =>
        {
            var centre = origin + box / 2f;
            var radius = (MathF.Min(box.X, box.Y) - thickness) / 2f;
            const float top = -MathF.PI / 2f;
            if (value is { } v)
            {
                paint.Renderer.DrawArc(centre, radius, thickness, 0f, MathF.Tau, track);
                if (v > 0f)
                {
                    paint.Renderer.DrawArc(centre, radius, thickness, top, MathF.Tau * Math.Clamp(v, 0f, 1f), indicator);
                }
                return;
            }
            // The arc grows from 10° to 270° and back every 1.3 s while the whole turns once a second.
            var phase = (float)(t % 1.3 / 1.3);
            var sweep = 0.17f + 4.54f * (0.5f - 0.5f * MathF.Cos(phase * MathF.Tau));
            var start = (float)(t * MathF.Tau % MathF.Tau) + top;
            paint.Renderer.DrawArc(centre, radius, thickness, start, sweep, indicator);
        })
        {
            Layout = new LayoutStyle { Width = size, Height = size },
            Semantics = new Semantics
            {
                Role = SemanticsRole.ProgressIndicator,
                Label = Label,
                Value = value is { } p ? string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(p * 100)}%") : null,
            },
        };
    }
}
