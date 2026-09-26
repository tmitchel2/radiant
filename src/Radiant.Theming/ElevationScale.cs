using System.Collections.Generic;
using System.Numerics;
using Radiant.UI.Core;

namespace Radiant.Theming;

/// <summary>
/// The shadows of each <see cref="ElevationLevel"/>: a tight key shadow and a soft ambient one,
/// as Material 3's web components draw them. The colour comes from the theme's shadow colour.
/// </summary>
public sealed record ElevationScale
{
    /// <summary>Material 3's elevation shadows: (offset y, blur, spread) of the key and ambient shadows per level.</summary>
    public IReadOnlyList<(float KeyY, float KeyBlur, float AmbientY, float AmbientBlur, float AmbientSpread)> Levels { get; init; } =
    [
        (0, 0, 0, 0, 0),
        (1, 2, 1, 3, 1),
        (1, 2, 2, 6, 2),
        (1, 3, 4, 8, 3),
        (2, 3, 6, 10, 4),
        (4, 4, 8, 12, 6),
    ];

    /// <summary>The key shadow's opacity.</summary>
    public float KeyOpacity { get; init; } = 0.3f;

    /// <summary>The ambient shadow's opacity.</summary>
    public float AmbientOpacity { get; init; } = 0.15f;

    /// <summary>A level's shadows in a colour (none for level 0).</summary>
    public IReadOnlyList<BoxShadow> Shadows(ElevationLevel level, Vector4 color)
    {
        var index = (int)level;
        if (index <= 0 || index >= Levels.Count)
        {
            return [];
        }
        var (keyY, keyBlur, ambientY, ambientBlur, ambientSpread) = Levels[index];
        return
        [
            new BoxShadow(new Vector2(0, ambientY), ambientBlur, ambientSpread, color with { W = color.W * AmbientOpacity }),
            new BoxShadow(new Vector2(0, keyY), keyBlur, 0f, color with { W = color.W * KeyOpacity }),
        ];
    }
}
