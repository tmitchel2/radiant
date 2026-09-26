// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-selectors.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Selects the nearest edge by its perpendicular distance, as one channel. The error correction
/// uses it for the exact distance a corrected texel is compared against.
/// </summary>
internal struct PerpendicularDistanceSelector : IEdgeSelector<PerpendicularDistanceSelector, double>
{
    private PerpendicularDistanceSelectorBase _base;
    private Vector2d _p;

    public static double Farthest => -double.MaxValue;

    public static PerpendicularDistanceSelector Create() => new() { _base = PerpendicularDistanceSelectorBase.Create() };

    public static double Resolve(in double distance) => distance;

    public void Reset(Vector2d p)
    {
        var delta = PerpendicularDistanceSelectorBase.DistanceDeltaFactor * (p - _p).Length;
        _base.Reset(delta);
        _p = p;
    }

    public void AddEdge(ref EdgeCache cache, EdgeSegment edge)
    {
        if (_base.IsEdgeRelevant(ref cache, PerpendicularDistanceSelectorBase.DistanceDeltaFactor * (_p - cache.Point).Length))
        {
            var distance = edge.GetSignedDistance(_p, out var param);
            _base.AddEdgeTrueDistance(edge, distance, param);
            cache.Point = _p;
            cache.AbsDistance = System.Math.Abs(distance.Distance);

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
                    _base.AddEdgePerpendicularDistance(pd = -pd);
                }
                cache.APerpendicularDistance = pd;
            }
            if (bdd > 0)
            {
                var pd = distance.Distance;
                if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, bp, bDir))
                {
                    _base.AddEdgePerpendicularDistance(pd);
                }
                cache.BPerpendicularDistance = pd;
            }
            cache.ADomainDistance = add;
            cache.BDomainDistance = bdd;
        }
    }

    public void Merge(ref PerpendicularDistanceSelector other) => _base.Merge(ref other._base);

    public readonly double Distance() => _base.ComputeDistance(_p);
}
