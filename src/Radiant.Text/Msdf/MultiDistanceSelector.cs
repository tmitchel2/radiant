// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-selectors.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Selects the nearest edge for each of the three channels by its perpendicular distance, each
/// channel seeing only the edges coloured with it. This is what makes a multi-channel field.
/// </summary>
internal struct MultiDistanceSelector : IEdgeSelector<MultiDistanceSelector, MultiDistance>
{
    private Vector2d _p;
    private PerpendicularDistanceSelectorBase _r;
    private PerpendicularDistanceSelectorBase _g;
    private PerpendicularDistanceSelectorBase _b;

    public static MultiDistance Farthest => new(-double.MaxValue, -double.MaxValue, -double.MaxValue);

    public static MultiDistanceSelector Create() => new()
    {
        _r = PerpendicularDistanceSelectorBase.Create(),
        _g = PerpendicularDistanceSelectorBase.Create(),
        _b = PerpendicularDistanceSelectorBase.Create(),
    };

    public static double Resolve(in MultiDistance distance) => distance.Median;

    public void Reset(Vector2d p)
    {
        var delta = PerpendicularDistanceSelectorBase.DistanceDeltaFactor * (p - _p).Length;
        _r.Reset(delta);
        _g.Reset(delta);
        _b.Reset(delta);
        _p = p;
    }

    public void AddEdge(ref EdgeCache cache, EdgeSegment edge)
    {
        var color = edge.Color;
        var red = (color & EdgeColor.Red) != 0;
        var green = (color & EdgeColor.Green) != 0;
        var blue = (color & EdgeColor.Blue) != 0;
        // msdfgen computes this step inside each channel's relevance test; it is the same for all three.
        var delta = PerpendicularDistanceSelectorBase.DistanceDeltaFactor * (_p - cache.Point).Length;
        if (
            (red && _r.IsEdgeRelevant(ref cache, delta)) ||
            (green && _g.IsEdgeRelevant(ref cache, delta)) ||
            (blue && _b.IsEdgeRelevant(ref cache, delta)))
        {
            var distance = edge.GetSignedDistance(_p, out var param);
            if (red)
            {
                _r.AddEdgeTrueDistance(edge, distance, param);
            }
            if (green)
            {
                _g.AddEdgeTrueDistance(edge, distance, param);
            }
            if (blue)
            {
                _b.AddEdgeTrueDistance(edge, distance, param);
            }
            cache.Point = _p;
            cache.AbsDistance = Math.Abs(distance.Distance);

            var ap = _p - cache.A;
            var bp = _p - cache.B;
            var aDir = cache.ADir;
            var bDir = cache.BDir;
            var add = Vector2d.Dot(ap, cache.AAxis);
            var bdd = -Vector2d.Dot(bp, cache.BAxis);
            if (add > 0)
            {
                var pd = distance.Distance;
                if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, ap, -aDir))
                {
                    pd = -pd;
                    AddPerpendicular(red, green, blue, pd);
                }
                cache.APerpendicularDistance = pd;
            }
            if (bdd > 0)
            {
                var pd = distance.Distance;
                if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, bp, bDir))
                {
                    AddPerpendicular(red, green, blue, pd);
                }
                cache.BPerpendicularDistance = pd;
            }
            cache.ADomainDistance = add;
            cache.BDomainDistance = bdd;
        }
    }

    public void Merge(ref MultiDistanceSelector other)
    {
        _r.Merge(ref other._r);
        _g.Merge(ref other._g);
        _b.Merge(ref other._b);
    }

    public readonly MultiDistance Distance() => new(_r.ComputeDistance(_p), _g.ComputeDistance(_p), _b.ComputeDistance(_p));

    private void AddPerpendicular(bool red, bool green, bool blue, double pd)
    {
        if (red)
        {
            _r.AddEdgePerpendicularDistance(pd);
        }
        if (green)
        {
            _g.AddEdgePerpendicularDistance(pd);
        }
        if (blue)
        {
            _b.AddEdgePerpendicularDistance(pd);
        }
    }
}
