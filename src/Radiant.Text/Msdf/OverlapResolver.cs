using System;
using System.Collections.Generic;

namespace Radiant.Text.Msdf;

/// <summary>
/// Rewrites a shape whose contours cross (each other or themselves) as its outline alone: the
/// boundary of the area the non-zero rule fills. This is Radiant's; msdfgen does the same with
/// Skia's path simplification (its <c>resolveShapeGeometry</c>), which is not available here.
/// <para>
/// Variable fonts keep overlapping strokes, and their contours cross in ways msdfgen's
/// overlapping-contour combiner does not untangle. A contour can cross itself (Inter's heavy 'A'
/// turns its counter's two sides across each other below the apex, and several letters' bowls
/// cross their stems in one contour), and a filled contour can overlap a hole (the heavy 'Q''s
/// tail crosses its counter). Every piece of such a contour would be taken for a border between
/// inside and out, and the glyph cut along it.
/// </para>
/// <para>
/// So every edge is cut where any other crosses it (<see cref="BezierIntersector"/>), and each
/// piece is kept only if the area on one side of it is filled and on the other is not, judged by
/// the original contours' winding a hair to either side of its middle. Kept pieces are turned so
/// the filled side is on their right, and joined end to end into loops. A shape with no crossings
/// is left exactly as it is, so it is generated exactly as msdfgen generates it; if the pieces
/// can't be joined into closed loops (degenerate geometry), the shape is left as it was too.
/// </para>
/// </summary>
internal static class OverlapResolver
{
    // Crossings nearer than this to an end of an edge (in its parameter) are at that end.
    private const double EndEpsilon = 1e-9;

    // More crossings than this means degenerate geometry (edges lying along each other).
    private const int MaxCrossings = 1024;

    /// <summary>Replaces the contours of <paramref name="shape"/> with the outline of what they fill, if they cross.</summary>
    public static void Resolve(Shape shape)
    {
        var (l, b, r, t) = shape.GetBounds();
        var size = Math.Max(r - l, t - b);
        if (!(size > 0))
        {
            return;
        }
        var contours = shape.Contours;
        var edges = new List<EdgeSegment>();
        var contourOf = new List<int>();
        var firstEdge = new int[contours.Count];
        for (var c = 0; c < contours.Count; c++)
        {
            firstEdge[c] = edges.Count;
            foreach (var edge in contours[c].Edges)
            {
                edges.Add(edge);
                contourOf.Add(c);
            }
        }

        // Where the edge after a given one starts: a crossing at an edge's end is at that start.
        int Next(int edge)
        {
            var c = contourOf[edge];
            var count = contours[c].Edges.Count;
            return firstEdge[c] + (edge - firstEdge[c] + 1) % count;
        }
        Position At(int edge, double param) =>
            param >= 1 - EndEpsilon ? new Position(Next(edge), 0) : new Position(edge, param <= EndEpsilon ? 0 : param);

        // Every crossing between two edges, as a pair of positions.
        var tolerance = 1e-7 * size;
        var pairs = new List<(Position A, Position B, Vector2d Point)>();
        var found = new List<(double Ta, double Tb)>();
        for (var i = 0; i < edges.Count; i++)
        {
            for (var j = i + 1; j < edges.Count; j++)
            {
                found.Clear();
                BezierIntersector.Intersect(edges[i], edges[j], tolerance, found);
                foreach (var (ta, tb) in found)
                {
                    var a = At(i, ta);
                    var bPosition = At(j, tb);
                    if (a == bPosition)
                    {
                        continue; // where consecutive edges meet
                    }
                    pairs.Add((a, bPosition, edges[i].Point(ta)));
                    if (pairs.Count > MaxCrossings)
                    {
                        return;
                    }
                }
            }
        }
        if (pairs.Count == 0)
        {
            return;
        }

        // Positions a hair apart on an edge are one; positions of one crossing are one node.
        var nodes = new Nodes(edges);
        foreach (var (a, bPosition, point) in pairs)
        {
            nodes.Join(nodes.Add(a, point), nodes.Add(bPosition, point));
        }

        // The pieces of each contour between its cuts, and whether each is part of the outline.
        var cutContours = new bool[contours.Count];
        var cuts = nodes.CutsByEdge();
        for (var e = 0; e < edges.Count; e++)
        {
            cutContours[contourOf[e]] |= cuts[e].Count > 0;
        }
        var kept = new List<EdgeSegment>();
        // Edges of the original contours are reversed only once every piece is classified, since
        // classifying uses their directions.
        var reverse = new List<EdgeSegment>();
        Span<Vector2d> control = stackalloc Vector2d[4];
        Span<Vector2d> section = stackalloc Vector2d[4];
        for (var c = 0; c < contours.Count; c++)
        {
            var contour = contours[c];
            if (!cutContours[c])
            {
                // Nothing crosses it, so the same area is on each side all the way round.
                var side = Side(edges, contour.Edges[0], size);
                if (side != 0)
                {
                    kept.AddRange(contour.Edges);
                    if (side < 0)
                    {
                        reverse.AddRange(contour.Edges);
                    }
                }
                continue;
            }
            for (var e = firstEdge[c]; e < firstEdge[c] + contour.Edges.Count; e++)
            {
                var edge = edges[e];
                edge.CopyControlPoints(control);
                var points = control[..(edge.Type + 1)];
                var t0 = 0.0;
                var start = nodes.StartOf(e);
                foreach (var (tCut, node) in cuts[e])
                {
                    if (tCut == 0)
                    {
                        continue;
                    }
                    var end = nodes.Point(node);
                    Keep(Piece(points, t0, tCut, start, end, section));
                    start = end;
                    t0 = tCut;
                }
                Keep(Piece(points, t0, 1, start, nodes.StartOf(Next(e)), section));
            }
        }

        void Keep(EdgeSegment? piece)
        {
            if (piece == null)
            {
                return;
            }
            var side = Side(edges, piece, size);
            if (side < 0)
            {
                piece.Reverse();
            }
            if (side != 0)
            {
                kept.Add(piece);
            }
        }

        foreach (var edge in reverse)
        {
            edge.Reverse();
        }
        var loops = Chain(kept);
        if (loops == null)
        {
            return;
        }
        // Loops with no area (edges that touched rather than crossed) fill nothing.
        loops.RemoveAll(loop => Math.Abs(Area(loop)) <= 1e-9 * size * size);
        contours.Clear();
        contours.AddRange(loops);
    }

    /// <summary>
    /// Whether a piece is part of the outline: 1 if the area on its right is filled and on its
    /// left is not, -1 the other way round, 0 if the same on both sides (inside an overlap, or
    /// outside everything).
    /// </summary>
    private static int Side(List<EdgeSegment> edges, EdgeSegment piece, double size)
    {
        var middle = piece.Point(.5);
        var left = piece.Direction(.5).Normalize().GetOrthogonal(true);
        var offset = 1e-5 * size;
        var leftFilled = Winding(edges, middle + offset * left) != 0;
        var rightFilled = Winding(edges, middle - offset * left) != 0;
        return leftFilled == rightFilled ? 0 : rightFilled ? 1 : -1;
    }

    /// <summary>The winding number of the original contours around a point: the directions of the crossings of a line to its left.</summary>
    private static int Winding(List<EdgeSegment> edges, Vector2d point)
    {
        Span<double> x = stackalloc double[3];
        Span<int> dy = stackalloc int[3];
        var winding = 0;
        foreach (var edge in edges)
        {
            var count = edge.ScanlineIntersections(x, dy, point.Y);
            for (var k = 0; k < count; k++)
            {
                if (x[k] <= point.X)
                {
                    winding += dy[k];
                }
            }
        }
        return winding;
    }

    /// <summary>Joins pieces end to end into closed loops; null if they don't close.</summary>
    private static List<Contour>? Chain(List<EdgeSegment> pieces)
    {
        var outgoing = new Dictionary<Vector2d, List<int>>();
        for (var i = 0; i < pieces.Count; i++)
        {
            var start = pieces[i].Point(0);
            if (!outgoing.TryGetValue(start, out var list))
            {
                outgoing[start] = list = [];
            }
            // Two contours can share an edge exactly (both borders of the same fill): keep one.
            if (!list.Exists(k => SameEdge(pieces[k], pieces[i])))
            {
                list.Add(i);
            }
        }
        var used = new bool[pieces.Count];
        var loops = new List<Contour>();
        foreach (var list in outgoing.Values)
        {
            foreach (var first in list)
            {
                if (used[first])
                {
                    continue;
                }
                var loop = new Contour();
                var origin = pieces[first].Point(0);
                var current = first;
                while (true)
                {
                    used[current] = true;
                    loop.Edges.Add(pieces[current]);
                    var end = pieces[current].Point(1);
                    if (end == origin)
                    {
                        break;
                    }
                    var index = outgoing.TryGetValue(end, out var next) ? next.FindIndex(k => !used[k]) : -1;
                    if (index < 0)
                    {
                        return null;
                    }
                    current = next![index];
                }
                loops.Add(loop);
            }
        }
        return loops;
    }

    private static bool SameEdge(EdgeSegment a, EdgeSegment b)
    {
        if (a.Type != b.Type)
        {
            return false;
        }
        Span<Vector2d> pa = stackalloc Vector2d[4];
        Span<Vector2d> pb = stackalloc Vector2d[4];
        a.CopyControlPoints(pa);
        b.CopyControlPoints(pb);
        return pa[..(a.Type + 1)].SequenceEqual(pb[..(b.Type + 1)]);
    }

    /// <summary>
    /// The piece of a curve between two parameters, with its ends put exactly at the given points;
    /// null if it has no length (a crossing exactly at a vertex).
    /// </summary>
    private static EdgeSegment? Piece(ReadOnlySpan<Vector2d> control, double t0, double t1, Vector2d start, Vector2d end, Span<Vector2d> section)
    {
        if (start == end)
        {
            return null;
        }
        BezierIntersector.Section(control, t0, t1, section);
        return control.Length switch
        {
            2 => EdgeSegment.Create(start, end),
            3 => EdgeSegment.Create(start, section[1], end),
            _ => EdgeSegment.Create(start, section[1], section[2], end),
        };
    }

    /// <summary>The loop's signed area, from points along its edges (enough to tell a real loop from none).</summary>
    private static double Area(Contour loop)
    {
        double area = 0;
        var previous = loop.Edges[^1].Point(1);
        foreach (var edge in loop.Edges)
        {
            for (var k = 1; k <= 8; k++)
            {
                var point = edge.Point(k / 8.0);
                area += Vector2d.Cross(previous, point);
                previous = point;
            }
        }
        return .5 * area;
    }

    /// <summary>A place on an edge: its index among all the shape's edges and a parameter, 0 ≤ t &lt; 1.</summary>
    private readonly record struct Position(int Edge, double T);

    /// <summary>
    /// The crossing positions, merged into nodes: positions a hair apart on one edge are one, and
    /// the positions where two edges cross are one node (a union-find). A node has one point, which
    /// every piece ending there ends at exactly, so the pieces join.
    /// </summary>
    private sealed class Nodes(List<EdgeSegment> edges)
    {
        private readonly List<Position> _positions = [];
        private readonly List<int> _parent = [];
        private readonly List<Vector2d> _points = [];

        public int Add(Position position, Vector2d point)
        {
            for (var k = 0; k < _positions.Count; k++)
            {
                if (_positions[k].Edge == position.Edge && Math.Abs(_positions[k].T - position.T) <= 1e-7)
                {
                    return k;
                }
            }
            _positions.Add(position);
            _parent.Add(_positions.Count - 1);
            // A node at an edge's start is that vertex exactly, so the edges meeting there still meet.
            _points.Add(position.T == 0 ? edges[position.Edge].Point(0) : point);
            return _positions.Count - 1;
        }

        public void Join(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b)
            {
                // A vertex stays where it is: if either is one, it is the node's point.
                if (_positions[a].T == 0)
                {
                    _parent[b] = a;
                }
                else
                {
                    _parent[a] = b;
                }
            }
        }

        public Vector2d Point(int node) => _points[Find(node)];

        /// <summary>Where edge <paramref name="edge"/> starts: its first vertex, or the node there.</summary>
        public Vector2d StartOf(int edge)
        {
            for (var k = 0; k < _positions.Count; k++)
            {
                if (_positions[k].Edge == edge && _positions[k].T == 0)
                {
                    return Point(k);
                }
            }
            return edges[edge].Point(0);
        }

        /// <summary>Each edge's cuts in order along it: the parameter and the node.</summary>
        public List<(double T, int Node)>[] CutsByEdge()
        {
            var cuts = new List<(double T, int Node)>[edges.Count];
            for (var e = 0; e < edges.Count; e++)
            {
                cuts[e] = [];
            }
            for (var k = 0; k < _positions.Count; k++)
            {
                cuts[_positions[k].Edge].Add((_positions[k].T, Find(k)));
            }
            foreach (var list in cuts)
            {
                list.Sort((x, y) => x.T.CompareTo(y.T));
            }
            return cuts;
        }

        private int Find(int k)
        {
            while (_parent[k] != k)
            {
                k = _parent[k] = _parent[_parent[k]];
            }
            return k;
        }
    }
}
