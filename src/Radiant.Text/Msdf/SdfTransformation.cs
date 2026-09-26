// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/Projection.cpp,
// core/DistanceMapping.cpp and core/SDFTransformation.h.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

namespace Radiant.Text.Msdf;

/// <summary>
/// Where a distance field's texels are in the shape, and how distances are stored in them. A texel
/// (x, y) samples the shape at ((x + ½, y + ½) / scale − translate), with row 0 at the bottom as
/// msdfgen orders them; a distance d in shape units is stored as (d − lower) / (upper − lower),
/// so the outline is at ½.
/// </summary>
/// <param name="Scale">Texels per shape unit on each axis.</param>
/// <param name="Translate">Added to shape coordinates before scaling.</param>
/// <param name="RangeLower">The distance, in shape units, stored as 0 (negative: outside).</param>
/// <param name="RangeUpper">The distance stored as 1.</param>
internal readonly record struct SdfTransformation(Vector2d Scale, Vector2d Translate, double RangeLower, double RangeUpper)
{
    /// <summary>A symmetrical range: distances from −width/2 to +width/2 shape units are stored.</summary>
    public static SdfTransformation Symmetrical(Vector2d scale, Vector2d translate, double rangeWidth) =>
        new(scale, translate, -.5 * rangeWidth, .5 * rangeWidth);

    /// <summary>From shape coordinates to texel coordinates.</summary>
    public Vector2d Project(Vector2d coord) => new(Scale.X * (coord.X + Translate.X), Scale.Y * (coord.Y + Translate.Y));

    /// <summary>From texel coordinates to shape coordinates.</summary>
    public Vector2d Unproject(Vector2d coord) => new(coord.X / Scale.X - Translate.X, coord.Y / Scale.Y - Translate.Y);

    /// <summary>A vector from texel to shape units.</summary>
    public Vector2d UnprojectVector(Vector2d vector) => new(vector.X / Scale.X, vector.Y / Scale.Y);

    /// <summary>A distance in shape units as the value stored for it.</summary>
    public double MapDistance(double distance) => 1 / (RangeUpper - RangeLower) * (distance + -RangeLower);

    /// <summary>A change of distance in shape units as the change of stored value.</summary>
    public double MapDelta(double delta) => 1 / (RangeUpper - RangeLower) * delta;
}
