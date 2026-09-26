using System;

namespace Radiant.Animation;

/// <summary>
/// A timing curve: a CSS-style cubic Bézier from (0, 0) to (1, 1) through two control points.
/// <see cref="Evaluate"/> maps elapsed time to progress.
/// </summary>
/// <param name="X1">First control point x.</param>
/// <param name="Y1">First control point y.</param>
/// <param name="X2">Second control point x.</param>
/// <param name="Y2">Second control point y.</param>
public readonly record struct Easing(float X1, float Y1, float X2, float Y2)
{
    /// <summary>Constant speed.</summary>
    public static Easing Linear { get; } = new(0f, 0f, 1f, 1f);

    /// <summary>Material's standard easing, for most transitions within the screen.</summary>
    public static Easing Standard { get; } = new(0.2f, 0f, 0f, 1f);

    /// <summary>Standard, decelerating: things arriving.</summary>
    public static Easing StandardDecelerate { get; } = new(0f, 0f, 0f, 1f);

    /// <summary>Standard, accelerating: things leaving.</summary>
    public static Easing StandardAccelerate { get; } = new(0.3f, 0f, 1f, 1f);

    /// <summary>Emphasised, decelerating: expressive arrivals.</summary>
    public static Easing EmphasizedDecelerate { get; } = new(0.05f, 0.7f, 0.1f, 1f);

    /// <summary>Emphasised, accelerating: expressive departures.</summary>
    public static Easing EmphasizedAccelerate { get; } = new(0.3f, 0f, 0.8f, 0.15f);

    /// <summary>Progress, 0 to 1, at time <paramref name="t"/> (0 to 1).</summary>
    public float Evaluate(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (t is 0f or 1f)
        {
            return t;
        }
        // Solve x(u) = t for the curve parameter u (Newton, falling back to bisection), then y(u).
        var u = t;
        for (var i = 0; i < 8; i++)
        {
            var error = Bezier(u, X1, X2) - t;
            if (MathF.Abs(error) < 1e-5f)
            {
                return Bezier(u, Y1, Y2);
            }
            var slope = Slope(u, X1, X2);
            if (MathF.Abs(slope) < 1e-6f)
            {
                break;
            }
            u -= error / slope;
        }
        float lo = 0f, hi = 1f;
        u = t;
        for (var i = 0; i < 32; i++)
        {
            if (Bezier(u, X1, X2) < t)
            {
                lo = u;
            }
            else
            {
                hi = u;
            }
            u = (lo + hi) / 2f;
        }
        return Bezier(u, Y1, Y2);
    }

    private static float Bezier(float u, float p1, float p2)
    {
        var v = 1f - u;
        return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
    }

    private static float Slope(float u, float p1, float p2)
    {
        var v = 1f - u;
        return 3f * v * v * p1 + 6f * v * u * (p2 - p1) + 3f * u * u * (1f - p2);
    }
}
