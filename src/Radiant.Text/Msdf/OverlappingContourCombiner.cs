// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/contour-combiners.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Combines the distances to each contour into the distance to the shape, choosing only contours
/// that actually border filled and unfilled area. Where two filled contours overlap, as the
/// contours of variable fonts do, the edge of one inside the other is not a border: taking the
/// nearest edge regardless would put a seam of "outside" through the overlap.
/// </summary>
/// <typeparam name="TSelector">How each contour's distance is chosen.</typeparam>
/// <typeparam name="TDistance">The distance that produces.</typeparam>
internal sealed class OverlappingContourCombiner<TSelector, TDistance>
    where TSelector : struct, IEdgeSelector<TSelector, TDistance>
    where TDistance : struct
{
    private readonly int[] _windings;
    private readonly TSelector[] _edgeSelectors;
    private readonly TDistance[] _contourDistances;
    private Vector2d _p;

    public OverlappingContourCombiner(Shape shape)
    {
        var count = shape.Contours.Count;
        _windings = new int[count];
        _edgeSelectors = new TSelector[count];
        _contourDistances = new TDistance[count];
        for (var i = 0; i < count; i++)
        {
            _windings[i] = shape.Contours[i].Winding();
            _edgeSelectors[i] = TSelector.Create();
        }
    }

    public void Reset(Vector2d p)
    {
        _p = p;
        for (var i = 0; i < _edgeSelectors.Length; i++)
        {
            _edgeSelectors[i].Reset(p);
        }
    }

    /// <summary>The selector that contour <paramref name="i"/>'s edges are offered to.</summary>
    public ref TSelector EdgeSelector(int i) => ref _edgeSelectors[i];

    public TDistance Distance()
    {
        if (_edgeSelectors.Length == 1)
        {
            // With one contour every branch below returns that contour's distance: merged into a
            // fresh selector it is unchanged, and the fresh selectors it is compared with are
            // infinitely far. Returning it directly is the same result without the merging.
            return _edgeSelectors[0].Distance();
        }
        var contourCount = _edgeSelectors.Length;
        var shapeEdgeSelector = TSelector.Create();
        var innerEdgeSelector = TSelector.Create();
        var outerEdgeSelector = TSelector.Create();
        shapeEdgeSelector.Reset(_p);
        innerEdgeSelector.Reset(_p);
        outerEdgeSelector.Reset(_p);
        // Each contour's distance is computed once here; msdfgen recomputes it in each loop below,
        // with the same result.
        var contourDistances = _contourDistances;
        for (var i = 0; i < contourCount; ++i)
        {
            var edgeDistance = contourDistances[i] = _edgeSelectors[i].Distance();
            shapeEdgeSelector.Merge(ref _edgeSelectors[i]);
            if (_windings[i] > 0 && TSelector.Resolve(edgeDistance) >= 0)
            {
                innerEdgeSelector.Merge(ref _edgeSelectors[i]);
            }
            if (_windings[i] < 0 && TSelector.Resolve(edgeDistance) <= 0)
            {
                outerEdgeSelector.Merge(ref _edgeSelectors[i]);
            }
        }

        var shapeDistance = shapeEdgeSelector.Distance();
        var innerDistance = innerEdgeSelector.Distance();
        var outerDistance = outerEdgeSelector.Distance();
        var innerScalarDistance = TSelector.Resolve(innerDistance);
        var outerScalarDistance = TSelector.Resolve(outerDistance);
        var distance = TSelector.Farthest;

        int winding;
        if (innerScalarDistance >= 0 && Math.Abs(innerScalarDistance) <= Math.Abs(outerScalarDistance))
        {
            distance = innerDistance;
            winding = 1;
            for (var i = 0; i < contourCount; ++i)
            {
                if (_windings[i] > 0)
                {
                    var contourDistance = contourDistances[i];
                    if (Math.Abs(TSelector.Resolve(contourDistance)) < Math.Abs(outerScalarDistance)
                        && TSelector.Resolve(contourDistance) > TSelector.Resolve(distance))
                    {
                        distance = contourDistance;
                    }
                }
            }
        }
        else if (outerScalarDistance <= 0 && Math.Abs(outerScalarDistance) < Math.Abs(innerScalarDistance))
        {
            distance = outerDistance;
            winding = -1;
            for (var i = 0; i < contourCount; ++i)
            {
                if (_windings[i] < 0)
                {
                    var contourDistance = contourDistances[i];
                    if (Math.Abs(TSelector.Resolve(contourDistance)) < Math.Abs(innerScalarDistance)
                        && TSelector.Resolve(contourDistance) < TSelector.Resolve(distance))
                    {
                        distance = contourDistance;
                    }
                }
            }
        }
        else
        {
            return shapeDistance;
        }

        for (var i = 0; i < contourCount; ++i)
        {
            if (_windings[i] != winding)
            {
                var contourDistance = contourDistances[i];
                if (TSelector.Resolve(contourDistance) * TSelector.Resolve(distance) >= 0
                    && Math.Abs(TSelector.Resolve(contourDistance)) < Math.Abs(TSelector.Resolve(distance)))
                {
                    distance = contourDistance;
                }
            }
        }
        if (TSelector.Resolve(distance) == TSelector.Resolve(shapeDistance))
        {
            distance = shapeDistance;
        }
        return distance;
    }
}
