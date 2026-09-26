using System;
using System.Linq;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>A rating as five stars, filled up to it (with a half star for a half), read out as "4.5 out of 5 stars".</summary>
/// <param name="Rating">Stars out of five; it shows to the nearest half.</param>
public sealed record Stars(double Rating) : Component
{
    /// <summary>Each star's size.</summary>
    public float Size { get; init; } = 18f;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        // In halves: 4.3 shows four and a half stars' worth rounded to 4.5, 4.2 shows 4.
        var halves = (int)Math.Round(Math.Clamp(Rating, 0, 5) * 2, MidpointRounding.AwayFromZero);
        var surface = context.UseSurface();
        var on = surface.With(new SurfaceChange { Content = SurfaceName.Tertiary });
        var off = surface with { Content = surface.Content with { Opacity = Legibility.Low } };
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Image, Label = $"{Rating:0.#} out of 5 stars" },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = [.. Enumerable.Range(0, 5).Select(i =>
            {
                var worth = Math.Clamp(halves - i * 2, 0, 2);
                return (Element?)ThemeContexts.Surface.Provide(worth > 0 ? on : off, new SurfaceIcon(worth == 1 ? "star_half" : "star") { IconSize = Size, IconFilled = worth > 0 });
            })],
        };
    }
}
