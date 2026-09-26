// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/ShapeDistanceFinder.hpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Finds the distance from a point to a shape, offering every edge (with its cache, which also
/// holds its geometry) to the selector of its contour. The caches make it fastest when each query
/// is near the last, as in a serpentine scan of the pixels.
/// </summary>
/// <typeparam name="TSelector">How each contour's distance is chosen.</typeparam>
/// <typeparam name="TDistance">The distance that produces.</typeparam>
internal sealed class ShapeDistanceFinder<TSelector, TDistance>
    where TSelector : struct, IEdgeSelector<TSelector, TDistance>
    where TDistance : struct
{
    private readonly Shape _shape;
    private readonly OverlappingContourCombiner<TSelector, TDistance> _contourCombiner;
    private readonly EdgeCache[] _shapeEdgeCache;

    public ShapeDistanceFinder(Shape shape)
    {
        _shape = shape;
        _contourCombiner = new OverlappingContourCombiner<TSelector, TDistance>(shape);
        _shapeEdgeCache = new EdgeCache[shape.EdgeCount];
        var cacheIndex = 0;
        foreach (var contour in shape.Contours)
        {
            var edges = contour.Edges;
            if (edges.Count == 0)
            {
                continue;
            }
            // In the order Distance visits them: each edge between the one before and after it.
            var prevEdge = edges.Count >= 2 ? edges[^2] : edges[0];
            var curEdge = edges[^1];
            foreach (var nextEdge in edges)
            {
                _shapeEdgeCache[cacheIndex++].Initialize(prevEdge, curEdge, nextEdge);
                prevEdge = curEdge;
                curEdge = nextEdge;
            }
        }
    }

    /// <summary>The distance from <paramref name="origin"/> to the shape.</summary>
    public TDistance Distance(Vector2d origin)
    {
        _contourCombiner.Reset(origin);
        var cacheIndex = 0;
        var contours = _shape.Contours;
        for (var c = 0; c < contours.Count; c++)
        {
            var edges = contours[c].Edges;
            if (edges.Count == 0)
            {
                continue;
            }
            ref var edgeSelector = ref _contourCombiner.EdgeSelector(c);
            // Each edge is offered as msdfgen offers it: the contour's last first, then the rest.
            edgeSelector.AddEdge(ref _shapeEdgeCache[cacheIndex++], edges[^1]);
            for (var e = 0; e < edges.Count - 1; e++)
            {
                edgeSelector.AddEdge(ref _shapeEdgeCache[cacheIndex++], edges[e]);
            }
        }
        return _contourCombiner.Distance();
    }
}
