// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/Shape.h and .cpp; the
// outline import follows ext/import-font.cpp and the orientation check follows main.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;
using System.Collections.Generic;

namespace Radiant.Text.Msdf;

/// <summary>
/// A glyph as msdfgen models it: contours of edge segments, in font units with y up. Built from a
/// <see cref="GlyphOutline"/>, then normalized, oriented and coloured before its distance field is
/// generated.
/// </summary>
internal sealed class Shape
{
    // Threshold of the dot product of adjacent edge directions to be considered convergent.
    private const double CornerDotEpsilon = .000001;

    // Moves control points slightly more than necessary to account for floating-point errors.
    private const double DeconvergeOvershoot = 1.11111111111111111;

    /// <summary>The contours.</summary>
    public List<Contour> Contours { get; } = [];

    /// <summary>The total number of edges.</summary>
    public int EdgeCount
    {
        get
        {
            var total = 0;
            foreach (var contour in Contours)
            {
                total += contour.Edges.Count;
            }
            return total;
        }
    }

    /// <summary>
    /// Builds a shape from an outline, as msdfgen imports a FreeType outline: an edge that goes
    /// nowhere is dropped, a curve whose control points are in line becomes a line, and every
    /// contour is closed back to its start whether or not the outline says so.
    /// </summary>
    public static Shape FromOutline(GlyphOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);
        var shape = new Shape();
        Contour? contour = null;
        Vector2d position = default, start = default;
        var points = outline.Points;
        var p = 0;
        foreach (var verb in outline.Verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    Close(contour, ref position, start);
                    if (contour is not { Edges.Count: 0 })
                    {
                        contour = new Contour();
                        shape.Contours.Add(contour);
                    }
                    position = start = Point(points[p++]);
                    break;
                case PathVerb.LineTo:
                {
                    var endpoint = Point(points[p++]);
                    if (contour != null && endpoint != position)
                    {
                        contour.Edges.Add(EdgeSegment.Create(position, endpoint));
                        position = endpoint;
                    }
                    break;
                }
                case PathVerb.QuadTo:
                {
                    var control = Point(points[p++]);
                    var endpoint = Point(points[p++]);
                    if (contour != null && endpoint != position)
                    {
                        contour.Edges.Add(EdgeSegment.Create(position, control, endpoint));
                        position = endpoint;
                    }
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var control1 = Point(points[p++]);
                    var control2 = Point(points[p++]);
                    var endpoint = Point(points[p++]);
                    if (contour != null && (endpoint != position || Vector2d.Cross(control1 - endpoint, control2 - endpoint) != 0))
                    {
                        contour.Edges.Add(EdgeSegment.Create(position, control1, control2, endpoint));
                        position = endpoint;
                    }
                    break;
                }
                case PathVerb.Close:
                    Close(contour, ref position, start);
                    break;
            }
        }
        Close(contour, ref position, start);
        if (shape.Contours.Count > 0 && shape.Contours[^1].Edges.Count == 0)
        {
            shape.Contours.RemoveAt(shape.Contours.Count - 1);
        }
        return shape;

        static Vector2d Point(System.Numerics.Vector2 v) => new(v.X, v.Y);

        static void Close(Contour? contour, ref Vector2d position, Vector2d start)
        {
            if (contour is { Edges.Count: > 0 } && position != start)
            {
                contour.Edges.Add(EdgeSegment.Create(position, start));
                position = start;
            }
        }
    }

    /// <summary>Whether every contour is closed: each edge starts where the one before it ends.</summary>
    public bool Validate()
    {
        foreach (var contour in Contours)
        {
            if (contour.Edges.Count > 0)
            {
                var corner = contour.Edges[^1].Point(1);
                foreach (var edge in contour.Edges)
                {
                    if (edge.Point(0) != corner)
                    {
                        return false;
                    }
                    corner = edge.Point(1);
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Prepares the geometry for a distance field: a contour of one edge is split in three (it
    /// needs three colours), and edges that leave a corner in exactly opposite directions are
    /// pushed apart, since at such a cusp the distance field cannot tell them apart.
    /// </summary>
    public void Normalize()
    {
        foreach (var contour in Contours)
        {
            var edges = contour.Edges;
            if (edges.Count == 1)
            {
                var (part0, part1, part2) = edges[0].SplitInThirds();
                edges.Clear();
                edges.Add(part0);
                edges.Add(part1);
                edges.Add(part2);
            }
            else if (edges.Count > 0)
            {
                // Push apart convergent edge segments
                var prevIndex = edges.Count - 1;
                for (var i = 0; i < edges.Count; i++)
                {
                    var prevDir = edges[prevIndex].Direction(1).Normalize();
                    var curDir = edges[i].Direction(0).Normalize();
                    if (Vector2d.Dot(prevDir, curDir) < CornerDotEpsilon - 1)
                    {
                        var factor = DeconvergeOvershoot * Math.Sqrt(1 - (CornerDotEpsilon - 1) * (CornerDotEpsilon - 1)) / (CornerDotEpsilon - 1);
                        var axis = factor * (curDir - prevDir).Normalize();
                        if (ConvergentCurveOrdering.Compute(edges[prevIndex], edges[i]) < 0)
                        {
                            axis = -axis;
                        }
                        edges[prevIndex] = DeconvergeEdge(edges[prevIndex], 1, axis.GetOrthogonal(true));
                        edges[i] = DeconvergeEdge(edges[i], 0, axis.GetOrthogonal(false));
                    }
                    prevIndex = i;
                }
            }
        }
    }

    /// <summary>The bounding box of every edge (control points of curves are not included unless on the curve).</summary>
    public (double Left, double Bottom, double Right, double Top) GetBounds()
    {
        const double largeValue = 1e240;
        double l = largeValue, b = largeValue, r = -largeValue, t = -largeValue;
        foreach (var contour in Contours)
        {
            contour.Bound(ref l, ref b, ref r, ref t);
        }
        return (l, b, r, t);
    }

    /// <summary>
    /// Reverses every contour if the shape is inside out: if a point well outside its bounds is at
    /// a positive (inside) distance. TrueType outlines wind filled contours one way and CFF
    /// outlines the other; the distance field wants positive distances inside.
    /// </summary>
    public void OrientOutward()
    {
        var (l, b, r, t) = GetBounds();
        var outerPoint = new Vector2d(l - (r - l) - 1, b - (t - b) - 1);
        var minDistance = SignedDistance.Infinite;
        foreach (var contour in Contours)
        {
            foreach (var edge in contour.Edges)
            {
                var distance = edge.GetSignedDistance(outerPoint, out _);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }
        }
        if (minDistance.Distance > 0)
        {
            foreach (var contour in Contours)
            {
                contour.Reverse();
            }
        }
    }

    private static EdgeSegment DeconvergeEdge(EdgeSegment edge, int param, Vector2d vector)
    {
        var cubic = edge switch
        {
            QuadraticSegment quadratic => quadratic.ConvertToCubic(),
            CubicSegment c => c,
            _ => null,
        };
        if (cubic == null)
        {
            return edge;
        }
        switch (param)
        {
            case 0:
                cubic.P1 += (cubic.P1 - cubic.P0).Length * vector;
                break;
            case 1:
                cubic.P2 += (cubic.P2 - cubic.P3).Length * vector;
                break;
        }
        return cubic;
    }
}
