using System.Collections.Generic;
using System.Numerics;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// The shadows of each <see cref="ElevationLevel"/>. The default is Material 3's, as its web
/// components draw them: a soft ambient shadow under a tight key shadow. The colour comes from
/// the theme's shadow colour.
/// </summary>
public sealed record ElevationScale
{
    /// <summary>Material 3's elevation shadows, levels 0 to 5: an ambient and a key shadow each.</summary>
    public IReadOnlyList<IReadOnlyList<ElevationShadow>> Levels { get; init; } =
    [
        [],
        Material(1, 2, 1, 3, 1),
        Material(1, 2, 2, 6, 2),
        Material(1, 3, 4, 8, 3),
        Material(2, 3, 6, 10, 4),
        Material(4, 4, 8, 12, 6),
    ];

    /// <summary>A level's shadows in a colour (none for level 0).</summary>
    public IReadOnlyList<BoxShadow> Shadows(ElevationLevel level, Vector4 color)
    {
        var index = (int)level;
        if (index <= 0 || index >= Levels.Count)
        {
            return [];
        }
        var shadows = Levels[index];
        var result = new BoxShadow[shadows.Count];
        for (var i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            result[i] = new BoxShadow(new Vector2(0, shadow.OffsetY), shadow.Blur, shadow.Spread, color with { W = color.W * shadow.Opacity });
        }
        return result;
    }

    // An ambient shadow at 15% under a key shadow at 30%.
    private static ElevationShadow[] Material(float keyY, float keyBlur, float ambientY, float ambientBlur, float ambientSpread) =>
        [new(ambientY, ambientBlur, ambientSpread, 0.15f), new(keyY, keyBlur, 0f, 0.3f)];
}

/// <summary>One shadow of an elevation level, cast straight down, as CSS's box-shadow gives it.</summary>
/// <param name="OffsetY">How far down it falls, in pixels.</param>
/// <param name="Blur">How far it blurs, in pixels.</param>
/// <param name="Spread">How much bigger (or, negative, smaller) than the box it is, in pixels.</param>
/// <param name="Opacity">The shadow colour's opacity.</param>
public readonly record struct ElevationShadow(float OffsetY, float Blur, float Spread, float Opacity);
