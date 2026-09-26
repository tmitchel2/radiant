// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8: the interface the edge
// selectors of core/edge-selectors.h share as C++ template parameters.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Picks, from the edges offered to it for one point, the distance a field stores there. The
/// selectors are structs used through generic type parameters, so the JIT specializes the
/// distance finder for each and the per-pixel inner loop makes no virtual calls.
/// </summary>
/// <typeparam name="TSelf">The selector.</typeparam>
/// <typeparam name="TDistance">What it produces: one distance, or one per channel.</typeparam>
internal interface IEdgeSelector<TSelf, TDistance>
    where TSelf : struct, IEdgeSelector<TSelf, TDistance>
    where TDistance : struct
{
    /// <summary>A selector that has seen no edges (msdfgen's default constructor).</summary>
    static abstract TSelf Create();

    /// <summary>A distance farther than any (msdfgen's <c>initDistance</c>).</summary>
    static abstract TDistance Farthest { get; }

    /// <summary>The single distance that decides the side of the edge a point is on (msdfgen's <c>resolveDistance</c>).</summary>
    static abstract double Resolve(in TDistance distance);

    /// <summary>Starts a new point, keeping what the last point's result bounds.</summary>
    void Reset(Vector2d p);

    /// <summary>Offers an edge, with its cache (which holds its geometry and that of its neighbours in the contour).</summary>
    void AddEdge(ref EdgeCache cache, EdgeSegment edge);

    /// <summary>Takes in another selector's candidates for the same point.</summary>
    void Merge(ref TSelf other);

    /// <summary>The distance at the current point.</summary>
    TDistance Distance();
}
