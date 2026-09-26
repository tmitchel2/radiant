// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/edge-selectors.h
// (PerpendicularDistanceSelectorBase::EdgeCache).
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// What was last computed for one edge, and where. Pixels are visited in a serpentine order, so
/// the next pixel is one step away; by the triangle inequality an edge's distance can only have
/// changed by that step, which rules out most edges without computing their distance again.
/// <para>
/// It also holds the edge's fixed geometry (<see cref="Initialize"/>): its ends, its directions
/// there and the bisectors with its neighbours' directions. msdfgen recomputes these each time the
/// edge is offered; computing them once gives the same values and saves most of that work.
/// </para>
/// </summary>
internal struct EdgeCache
{
    public Vector2d Point;
    public double AbsDistance;
    public double ADomainDistance;
    public double BDomainDistance;
    public double APerpendicularDistance;
    public double BPerpendicularDistance;

    /// <summary>The edge's start and end.</summary>
    public Vector2d A, B;

    /// <summary>The edge's unit directions at its start and end.</summary>
    public Vector2d ADir, BDir;

    /// <summary>The unit bisectors of the directions at each end and the neighbouring edges' there.</summary>
    public Vector2d AAxis, BAxis;

    /// <summary>Computes the fixed geometry of <paramref name="edge"/>, between <paramref name="prevEdge"/> and <paramref name="nextEdge"/>.</summary>
    public void Initialize(EdgeSegment prevEdge, EdgeSegment edge, EdgeSegment nextEdge)
    {
        A = edge.Point(0);
        B = edge.Point(1);
        ADir = edge.Direction(0).Normalize(true);
        BDir = edge.Direction(1).Normalize(true);
        var prevDir = prevEdge.Direction(1).Normalize(true);
        var nextDir = nextEdge.Direction(0).Normalize(true);
        AAxis = (prevDir + ADir).Normalize(true);
        BAxis = (BDir + nextDir).Normalize(true);
    }
}
