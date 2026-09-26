// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/Contour.h and .cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;
using System.Collections.Generic;

namespace Radiant.Text.Msdf;

/// <summary>
/// A closed loop of edges, each starting where the one before it ends. Its winding (which way
/// round it goes) says whether it adds filled area or cuts a hole, which is how overlapping
/// contours are told apart from nested ones.
/// </summary>
internal sealed class Contour
{
    /// <summary>The edges in order.</summary>
    public List<EdgeSegment> Edges { get; } = [];

    /// <summary>Grows a bounding box to fit the contour.</summary>
    public void Bound(ref double xMin, ref double yMin, ref double xMax, ref double yMax)
    {
        foreach (var edge in Edges)
        {
            edge.Bound(ref xMin, ref yMin, ref xMax, ref yMax);
        }
    }

    /// <summary>
    /// Which way the contour goes round: 1 clockwise (with y up), which is how filled contours
    /// are wound once the shape is oriented; -1 anticlockwise; 0 if it has no area.
    /// </summary>
    public int Winding()
    {
        if (Edges.Count == 0)
        {
            return 0;
        }
        double total = 0;
        if (Edges.Count == 1)
        {
            Vector2d a = Edges[0].Point(0), b = Edges[0].Point(1 / 3.0), c = Edges[0].Point(2 / 3.0);
            total += Shoelace(a, b);
            total += Shoelace(b, c);
            total += Shoelace(c, a);
        }
        else if (Edges.Count == 2)
        {
            Vector2d a = Edges[0].Point(0), b = Edges[0].Point(.5), c = Edges[1].Point(0), d = Edges[1].Point(.5);
            total += Shoelace(a, b);
            total += Shoelace(b, c);
            total += Shoelace(c, d);
            total += Shoelace(d, a);
        }
        else
        {
            var prev = Edges[^1].Point(0);
            foreach (var edge in Edges)
            {
                var cur = edge.Point(0);
                total += Shoelace(prev, cur);
                prev = cur;
            }
        }
        return Math.Sign(total);
    }

    /// <summary>Reverses the contour's direction.</summary>
    public void Reverse()
    {
        Edges.Reverse();
        foreach (var edge in Edges)
        {
            edge.Reverse();
        }
    }

    private static double Shoelace(Vector2d a, Vector2d b) => (b.X - a.X) * (a.Y + b.Y);
}
