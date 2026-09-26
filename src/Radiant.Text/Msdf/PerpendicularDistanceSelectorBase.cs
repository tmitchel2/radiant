// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-selectors.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// The perpendicular (pseudo-) distance for one channel: the distance to the nearest edge, but
/// measured to the edge's straight extension where the point is beyond its end. That is what
/// keeps a channel's zero line straight through a corner, so the median of three channels
/// meets at a sharp point.
/// </summary>
internal struct PerpendicularDistanceSelectorBase
{
    public const double DistanceDeltaFactor = 1.001;

    private SignedDistance _minTrueDistance;
    private double _minNegativePerpendicularDistance;
    private double _minPositivePerpendicularDistance;
    private EdgeSegment? _nearEdge;
    private double _nearEdgeParam;

    public static PerpendicularDistanceSelectorBase Create()
    {
        var selector = default(PerpendicularDistanceSelectorBase);
        selector._minTrueDistance = SignedDistance.Infinite;
        selector._minNegativePerpendicularDistance = -Math.Abs(selector._minTrueDistance.Distance);
        selector._minPositivePerpendicularDistance = Math.Abs(selector._minTrueDistance.Distance);
        return selector;
    }

    /// <summary>
    /// If the point is on the far side of an edge's end (<paramref name="ep"/> from it, along
    /// <paramref name="edgeDir"/>), replaces <paramref name="distance"/> with the distance to the
    /// edge's extension there if that is nearer.
    /// </summary>
    public static bool GetPerpendicularDistance(ref double distance, Vector2d ep, Vector2d edgeDir)
    {
        var ts = Vector2d.Dot(ep, edgeDir);
        if (ts > 0)
        {
            var perpendicularDistance = Vector2d.Cross(ep, edgeDir);
            if (Math.Abs(perpendicularDistance) < Math.Abs(distance))
            {
                distance = perpendicularDistance;
                return true;
            }
        }
        return false;
    }

    public readonly SignedDistance TrueDistance => _minTrueDistance;

    /// <summary>Starts a new point <paramref name="delta"/> from the last, keeping the last result grown by it as a bound.</summary>
    public void Reset(double delta)
    {
        _minTrueDistance = _minTrueDistance with { Distance = _minTrueDistance.Distance + NonZeroSign(_minTrueDistance.Distance) * delta };
        _minNegativePerpendicularDistance = -Math.Abs(_minTrueDistance.Distance);
        _minPositivePerpendicularDistance = Math.Abs(_minTrueDistance.Distance);
        _nearEdge = null;
        _nearEdgeParam = 0;
    }

    /// <summary>
    /// Whether an edge could still be nearer than what has been found, going by its cache and the
    /// step <paramref name="delta"/> (<see cref="DistanceDeltaFactor"/> × the distance from where
    /// the cache was made to the point).
    /// </summary>
    public readonly bool IsEdgeRelevant(ref EdgeCache cache, double delta)
    {
        return
            cache.AbsDistance - delta <= Math.Abs(_minTrueDistance.Distance) ||
            Math.Abs(cache.ADomainDistance) < delta ||
            Math.Abs(cache.BDomainDistance) < delta ||
            (cache.ADomainDistance > 0 && (cache.APerpendicularDistance < 0
                ? cache.APerpendicularDistance + delta >= _minNegativePerpendicularDistance
                : cache.APerpendicularDistance - delta <= _minPositivePerpendicularDistance)) ||
            (cache.BDomainDistance > 0 && (cache.BPerpendicularDistance < 0
                ? cache.BPerpendicularDistance + delta >= _minNegativePerpendicularDistance
                : cache.BPerpendicularDistance - delta <= _minPositivePerpendicularDistance));
    }

    public void AddEdgeTrueDistance(EdgeSegment edge, SignedDistance distance, double param)
    {
        if (distance < _minTrueDistance)
        {
            _minTrueDistance = distance;
            _nearEdge = edge;
            _nearEdgeParam = param;
        }
    }

    public void AddEdgePerpendicularDistance(double distance)
    {
        if (distance <= 0 && distance > _minNegativePerpendicularDistance)
        {
            _minNegativePerpendicularDistance = distance;
        }
        if (distance >= 0 && distance < _minPositivePerpendicularDistance)
        {
            _minPositivePerpendicularDistance = distance;
        }
    }

    public void Merge(ref PerpendicularDistanceSelectorBase other)
    {
        if (other._minTrueDistance < _minTrueDistance)
        {
            _minTrueDistance = other._minTrueDistance;
            _nearEdge = other._nearEdge;
            _nearEdgeParam = other._nearEdgeParam;
        }
        if (other._minNegativePerpendicularDistance > _minNegativePerpendicularDistance)
        {
            _minNegativePerpendicularDistance = other._minNegativePerpendicularDistance;
        }
        if (other._minPositivePerpendicularDistance < _minPositivePerpendicularDistance)
        {
            _minPositivePerpendicularDistance = other._minPositivePerpendicularDistance;
        }
    }

    public readonly double ComputeDistance(Vector2d p)
    {
        var minDistance = _minTrueDistance.Distance < 0 ? _minNegativePerpendicularDistance : _minPositivePerpendicularDistance;
        if (_nearEdge != null)
        {
            var distance = _minTrueDistance;
            _nearEdge.DistanceToPerpendicularDistance(ref distance, p, _nearEdgeParam);
            if (Math.Abs(distance.Distance) < Math.Abs(minDistance))
            {
                minDistance = distance.Distance;
            }
        }
        return minDistance;
    }

    private static int NonZeroSign(double n) => n > 0 ? 1 : -1;
}
