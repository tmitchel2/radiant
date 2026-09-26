// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/Vector2.hpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// A 2D vector in double precision. The distance field is computed in doubles, as msdfgen does,
/// because near-ties between edges are decided by comparing distances, and single precision
/// would decide some of them differently from the reference implementation.
/// </summary>
internal readonly record struct Vector2d(double X, double Y)
{
    /// <summary>Whether the vector is non-zero (msdfgen's conversion of a vector to bool).</summary>
    public bool IsNonZero => X != 0 || Y != 0;

    public double Length => Math.Sqrt(X * X + Y * Y);

    /// <summary>The unit vector in the same direction; a zero vector gives (0, 1), or (0, 0) if <paramref name="allowZero"/>.</summary>
    public Vector2d Normalize(bool allowZero = false)
    {
        var length = Length;
        return length != 0 ? new Vector2d(X / length, Y / length) : new Vector2d(0, allowZero ? 0 : 1);
    }

    /// <summary>A vector of the same length at right angles: anticlockwise if <paramref name="polarity"/>, else clockwise.</summary>
    public Vector2d GetOrthogonal(bool polarity = true) => polarity ? new Vector2d(-Y, X) : new Vector2d(Y, -X);

    /// <summary>A unit vector at right angles: anticlockwise if <paramref name="polarity"/>, else clockwise.</summary>
    public Vector2d GetOrthonormal(bool polarity = true, bool allowZero = false)
    {
        var length = Length;
        if (length != 0)
        {
            return polarity ? new Vector2d(-Y / length, X / length) : new Vector2d(Y / length, -X / length);
        }
        var y = allowZero ? 0 : 1;
        return polarity ? new Vector2d(0, y) : new Vector2d(0, -y);
    }

    public static double Dot(Vector2d a, Vector2d b) => a.X * b.X + a.Y * b.Y;

    public static double Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;

    /// <summary>The weighted average of two points (msdfgen's <c>mix</c>).</summary>
    public static Vector2d Mix(Vector2d a, Vector2d b, double weight) => (1 - weight) * a + weight * b;

    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2d operator -(Vector2d v) => new(-v.X, -v.Y);

    public static Vector2d operator *(double s, Vector2d v) => new(s * v.X, s * v.Y);

    public static Vector2d operator /(Vector2d v, double s) => new(v.X / s, v.Y / s);
}
