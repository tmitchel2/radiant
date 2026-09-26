// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/SignedDistance.hpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// A signed distance to an edge and how squarely the edge ends there. Two edges that meet at a
/// corner are the same distance from a point beyond it; the one whose end points more directly
/// away (the smaller <see cref="Dot"/>) is the one whose side the point is really on, so the pair
/// orders edges uniquely.
/// </summary>
/// <param name="Distance">The distance: positive inside the shape, negative outside.</param>
/// <param name="Dot">At an endpoint, |cos| of the angle between the edge and the way to the point; else 0.</param>
internal readonly record struct SignedDistance(double Distance, double Dot)
{
    /// <summary>Farther than anything (msdfgen's default-constructed distance).</summary>
    public static SignedDistance Infinite => new(-double.MaxValue, 0);

    public static bool operator <(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) < Math.Abs(b.Distance) || (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot < b.Dot);

    public static bool operator >(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) > Math.Abs(b.Distance) || (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot > b.Dot);

    public static bool operator <=(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) < Math.Abs(b.Distance) || (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot <= b.Dot);

    public static bool operator >=(SignedDistance a, SignedDistance b) =>
        Math.Abs(a.Distance) > Math.Abs(b.Distance) || (Math.Abs(a.Distance) == Math.Abs(b.Distance) && a.Dot >= b.Dot);
}
