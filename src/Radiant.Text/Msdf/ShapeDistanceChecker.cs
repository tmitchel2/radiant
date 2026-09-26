// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/MSDFErrorCorrection.cpp
// and core/bitmap-interpolation.hpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Finds the artifacts the field alone cannot show, by comparing the interpolated distance with
/// the exact distance to the shape, before and after a texel would be corrected. It is the
/// slower check, so it runs only on texels the first pass protected (those at edges).
/// </summary>
internal sealed class ShapeDistanceChecker
{
    private readonly ShapeDistanceFinder<PerpendicularDistanceSelector, double> _distanceFinder;
    private readonly float[] _sdf;
    private readonly int _width;
    private readonly int _height;
    private readonly SdfTransformation _transformation;
    private readonly Vector2d _texelSize;
    private readonly double _minImproveRatio;

    public ShapeDistanceChecker(float[] sdf, int width, int height, Shape shape, SdfTransformation transformation, double minImproveRatio)
    {
        _distanceFinder = new ShapeDistanceFinder<PerpendicularDistanceSelector, double>(shape);
        _sdf = sdf;
        _width = width;
        _height = height;
        _transformation = transformation;
        _minImproveRatio = minImproveRatio;
        _texelSize = transformation.UnprojectVector(new Vector2d(1, 1));
    }

    /// <summary>The texel being checked, in shape coordinates.</summary>
    public Vector2d ShapeCoord { get; set; }

    /// <summary>The texel being checked, in texel coordinates (its centre).</summary>
    public Vector2d SdfCoord { get; set; }

    /// <summary>The index of the texel's first channel.</summary>
    public int Msd { get; set; }

    /// <summary>Whether the texel is protected.</summary>
    public bool ProtectedFlag { get; set; }

    public ShapeDistanceArtifactClassifier Classifier(Vector2d direction, double span) => new(this, direction, span);

    /// <summary>Whether correcting the current texel would bring the median at <paramref name="t"/> along <paramref name="direction"/> nearer the true distance.</summary>
    public bool ImprovesByCorrecting(Vector2d direction, double t)
    {
        var tVector = t * direction;
        Span<float> oldMsd = stackalloc float[3];
        Span<float> newMsd = stackalloc float[3];
        // Compute the color that would be currently interpolated at the artifact candidate's position.
        var sdfCoord = SdfCoord + tVector;
        Interpolate(oldMsd, sdfCoord);
        // Compute the color that would be interpolated at the artifact candidate's position if error correction was applied on the current texel.
        var aWeight = (1 - Math.Abs(tVector.X)) * (1 - Math.Abs(tVector.Y));
        var msd = _sdf.AsSpan(Msd, 3);
        var aPsd = MsdfErrorCorrection.Median(msd[0], msd[1], msd[2]);
        newMsd[0] = (float)(oldMsd[0] + aWeight * (aPsd - msd[0]));
        newMsd[1] = (float)(oldMsd[1] + aWeight * (aPsd - msd[1]));
        newMsd[2] = (float)(oldMsd[2] + aWeight * (aPsd - msd[2]));
        // Compute the evaluated distance (interpolated median) before and after error correction, as well as the exact shape distance.
        var oldPsd = MsdfErrorCorrection.Median(oldMsd[0], oldMsd[1], oldMsd[2]);
        var newPsd = MsdfErrorCorrection.Median(newMsd[0], newMsd[1], newMsd[2]);
        var shapePoint = ShapeCoord + new Vector2d(tVector.X * _texelSize.X, tVector.Y * _texelSize.Y);
        var refPsd = (float)_transformation.MapDistance(_distanceFinder.Distance(shapePoint));
        // Compare the differences of the exact distance and the before and after distances.
        return _minImproveRatio * Math.Abs(newPsd - refPsd) < (double)Math.Abs(oldPsd - refPsd);
    }

    /// <summary>Bilinear interpolation of the field at <paramref name="pos"/> (texel coordinates), clamped to its edges.</summary>
    private void Interpolate(Span<float> output, Vector2d pos)
    {
        var x = ClampUnit(pos.X, _width) - .5;
        var y = ClampUnit(pos.Y, _height) - .5;
        var l = (int)Math.Floor(x);
        var b = (int)Math.Floor(y);
        var r = l + 1;
        var t = b + 1;
        var lr = x - l;
        var bt = y - b;
        l = Math.Clamp(l, 0, _width - 1);
        r = Math.Clamp(r, 0, _width - 1);
        b = Math.Clamp(b, 0, _height - 1);
        t = Math.Clamp(t, 0, _height - 1);
        for (var i = 0; i < 3; ++i)
        {
            output[i] = MsdfErrorCorrection.Mix(
                MsdfErrorCorrection.Mix(_sdf[(b * _width + l) * 3 + i], _sdf[(b * _width + r) * 3 + i], lr),
                MsdfErrorCorrection.Mix(_sdf[(t * _width + l) * 3 + i], _sdf[(t * _width + r) * 3 + i], lr),
                bt);
        }
    }

    // msdfgen's clamp(n, b): n if 0 <= n <= b, else 0 or b.
    private static double ClampUnit(double n, double b) => n >= 0 && n <= b ? n : n > 0 ? b : 0;
}
