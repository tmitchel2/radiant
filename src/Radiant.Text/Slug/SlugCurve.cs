using System.Numerics;

namespace Radiant.Text.Slug;

/// <summary>
/// One quadratic Bézier piece of a glyph's outline, in em units with y up: the only kind of curve
/// Slug evaluates. Lines are quadratics whose control point is their end point, as the Slug
/// reference recommends: that keeps the root solve well conditioned, where a midpoint control
/// point would make every line hit the near-linear case.
/// </summary>
/// <param name="P1">The start point.</param>
/// <param name="P2">The control point.</param>
/// <param name="P3">The end point, which is the next curve's start in the same contour.</param>
public readonly record struct SlugCurve(Vector2 P1, Vector2 P2, Vector2 P3)
{
    /// <summary>The smallest box around the three control points (which contains the curve).</summary>
    public Vector2 Min => Vector2.Min(Vector2.Min(P1, P2), P3);

    /// <summary>The largest corner of the box around the three control points.</summary>
    public Vector2 Max => Vector2.Max(Vector2.Max(P1, P2), P3);

    /// <summary>The point at parameter <paramref name="t"/>, from 0 at the start to 1 at the end.</summary>
    public Vector2 Evaluate(float t)
    {
        var u = 1f - t;
        return (u * u * P1) + (2f * u * t * P2) + (t * t * P3);
    }
}
