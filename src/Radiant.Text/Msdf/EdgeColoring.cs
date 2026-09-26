// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-coloring.cpp
// (edgeColoringSimple).
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;
using System.Collections.Generic;

namespace Radiant.Text.Msdf;

/// <summary>
/// Assigns edges to channels so that the two edges at every corner share at most one channel.
/// Each channel then sees the corner as the meeting of two straight extensions, and the median
/// of the three reproduces the sharp corner. Smooth joins keep their colour, so curves stay
/// smooth.
/// </summary>
internal static class EdgeColoring
{
    /// <summary>msdfgen's default angle threshold: joins turning by more than this (in radians) are corners.</summary>
    public const double DefaultAngleThreshold = 3.0;

    /// <summary>
    /// msdfgen's simple edge colouring: at each corner the colour switches, seeded
    /// deterministically, and a contour with a single corner (a teardrop) is split into three
    /// colours along its length.
    /// </summary>
    public static void Simple(Shape shape, double angleThreshold = DefaultAngleThreshold, ulong seed = 0)
    {
        var crossThreshold = Math.Sin(angleThreshold);
        var color = InitColor(ref seed);
        var corners = new List<int>();
        var colors = new EdgeColor[3];
        foreach (var contour in shape.Contours)
        {
            var edges = contour.Edges;
            if (edges.Count == 0)
            {
                continue;
            }
            {
                // Identify corners
                corners.Clear();
                var prevDirection = edges[^1].Direction(1);
                for (var index = 0; index < edges.Count; index++)
                {
                    var edge = edges[index];
                    if (IsCorner(prevDirection.Normalize(), edge.Direction(0).Normalize(), crossThreshold))
                    {
                        corners.Add(index);
                    }
                    prevDirection = edge.Direction(1);
                }
            }

            if (corners.Count == 0)
            {
                // Smooth contour
                SwitchColor(ref color, ref seed);
                foreach (var edge in edges)
                {
                    edge.Color = color;
                }
            }
            else if (corners.Count == 1)
            {
                // "Teardrop" case
                SwitchColor(ref color, ref seed);
                colors[0] = color;
                colors[1] = EdgeColor.White;
                SwitchColor(ref color, ref seed);
                colors[2] = color;
                var corner = corners[0];
                if (edges.Count >= 3)
                {
                    var m = edges.Count;
                    for (var i = 0; i < m; ++i)
                    {
                        edges[(corner + i) % m].Color = colors[1 + SymmetricalTrichotomy(i, m)];
                    }
                }
                else
                {
                    // Less than three edge segments for three colors => edges must be split
                    var parts = new EdgeSegment?[7];
                    (parts[0 + 3 * corner], parts[1 + 3 * corner], parts[2 + 3 * corner]) = edges[0].SplitInThirds();
                    if (edges.Count >= 2)
                    {
                        (parts[3 - 3 * corner], parts[4 - 3 * corner], parts[5 - 3 * corner]) = edges[1].SplitInThirds();
                        parts[0]!.Color = parts[1]!.Color = colors[0];
                        parts[2]!.Color = parts[3]!.Color = colors[1];
                        parts[4]!.Color = parts[5]!.Color = colors[2];
                    }
                    else
                    {
                        parts[0]!.Color = colors[0];
                        parts[1]!.Color = colors[1];
                        parts[2]!.Color = colors[2];
                    }
                    edges.Clear();
                    for (var i = 0; parts[i] is { } part; ++i)
                    {
                        edges.Add(part);
                    }
                }
            }
            else
            {
                // Multiple corners
                var cornerCount = corners.Count;
                var spline = 0;
                var start = corners[0];
                var m = edges.Count;
                SwitchColor(ref color, ref seed);
                var initialColor = color;
                for (var i = 0; i < m; ++i)
                {
                    var index = (start + i) % m;
                    if (spline + 1 < cornerCount && corners[spline + 1] == index)
                    {
                        ++spline;
                        SwitchColor(ref color, ref seed, spline == cornerCount - 1 ? initialColor : EdgeColor.Black);
                    }
                    edges[index].Color = color;
                }
            }
        }
    }

    /// <summary>
    /// For each position below <paramref name="n"/>: -1, 0 or 1 for whether it is nearer the
    /// beginning, middle or end. The total over all positions is zero.
    /// </summary>
    private static int SymmetricalTrichotomy(int position, int n) => (3 * position + 1) / n - 1;

    private static bool IsCorner(Vector2d aDir, Vector2d bDir, double crossThreshold) =>
        Vector2d.Dot(aDir, bDir) <= 0 || Math.Abs(Vector2d.Cross(aDir, bDir)) > crossThreshold;

    private static int SeedExtract2(ref ulong seed)
    {
        var v = (int)(seed & 1);
        seed >>= 1;
        return v;
    }

    private static int SeedExtract3(ref ulong seed)
    {
        var v = (int)(seed % 3);
        seed /= 3;
        return v;
    }

    private static EdgeColor InitColor(ref ulong seed)
    {
        ReadOnlySpan<EdgeColor> colors = [EdgeColor.Cyan, EdgeColor.Magenta, EdgeColor.Yellow];
        return colors[SeedExtract3(ref seed)];
    }

    private static void SwitchColor(ref EdgeColor color, ref ulong seed)
    {
        var shifted = (int)color << (1 + SeedExtract2(ref seed));
        color = (EdgeColor)((shifted | (shifted >> 3)) & (int)EdgeColor.White);
    }

    private static void SwitchColor(ref EdgeColor color, ref ulong seed, EdgeColor banned)
    {
        var combined = color & banned;
        if (combined is EdgeColor.Red or EdgeColor.Green or EdgeColor.Blue)
        {
            color = combined ^ EdgeColor.White;
        }
        else
        {
            SwitchColor(ref color, ref seed);
        }
    }
}
