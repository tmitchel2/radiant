// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/MSDFErrorCorrection.cpp and
// core/msdf-error-correction.cpp (the default mode: edge priority, checking the distance at edges).
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Removes the artifacts of a multi-channel field: places where bilinear interpolation between
/// texels makes the median cross ½ where no edge is, which shows as specks or notches. Texels
/// that cause one are made single-channel (all three set to their median), which cannot produce
/// such a crossing. Texels at corners and edges are protected, since flattening them would round
/// the corners MSDF exists to keep; for those, the exact shape distance decides.
/// </summary>
internal sealed class MsdfErrorCorrection
{
    /// <summary>The default minimum ratio between the actual and the largest expected change between texels to be an error.</summary>
    public const double DefaultMinDeviationRatio = 1.11111111111111111;

    /// <summary>The default minimum ratio by which a correction must bring the distance nearer the exact one.</summary>
    public const double DefaultMinImproveRatio = 1.11111111111111111;

    // Stencil flags: a texel that causes interpolation errors, and one that may only be flagged for inversions.
    private const byte Error = 1;
    private const byte Protected = 2;

    private const double ArtifactTEpsilon = .01;
    private const double ProtectionRadiusTolerance = 1.001;

    private readonly byte[] _stencil;
    private readonly int _width;
    private readonly int _height;
    private readonly SdfTransformation _transformation;

    private MsdfErrorCorrection(int width, int height, SdfTransformation transformation)
    {
        _stencil = new byte[width * height];
        _width = width;
        _height = height;
        _transformation = transformation;
    }

    /// <summary>
    /// Corrects a three-channel field in place, as msdfgen's default does: corners and edges are
    /// protected, the field is searched for artifacts, then every texel is protected and those
    /// left are checked against the exact distance to the shape.
    /// </summary>
    /// <param name="sdf">The field: three floats a texel, rows from the bottom.</param>
    public static void Apply(float[] sdf, int width, int height, Shape shape, SdfTransformation transformation)
    {
        var ec = new MsdfErrorCorrection(width, height, transformation);
        ec.ProtectCorners(shape);
        ec.ProtectEdges(sdf);
        ec.FindErrors(sdf);
        ec.ProtectAll();
        ec.FindErrors(sdf, shape);
        ec.ApplyStencil(sdf);
    }

    public static float Median(float a, float b, float c) => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));

    /// <summary>msdfgen's <c>mix</c> of two floats by a double weight, computed in double and stored as float.</summary>
    public static float Mix(float a, float b, double weight) => (float)((1 - weight) * a + weight * b);

    /// <summary>Flags the four texels around every corner (where the colour changes) as protected.</summary>
    private void ProtectCorners(Shape shape)
    {
        foreach (var contour in shape.Contours)
        {
            var edges = contour.Edges;
            if (edges.Count == 0)
            {
                continue;
            }
            var prevEdge = edges[^1];
            foreach (var edge in edges)
            {
                var commonColor = (int)(prevEdge.Color & edge.Color);
                // If the color changes from prevEdge to edge, this is a corner.
                if ((commonColor & (commonColor - 1)) == 0)
                {
                    // Find the four texels that envelop the corner and mark them as protected.
                    var p = _transformation.Project(edge.Point(0));
                    var l = (int)Math.Floor(p.X - .5);
                    var b = (int)Math.Floor(p.Y - .5);
                    var r = l + 1;
                    var t = b + 1;
                    // Check that the positions are within bounds.
                    if (l < _width && b < _height && r >= 0 && t >= 0)
                    {
                        if (l >= 0 && b >= 0)
                        {
                            _stencil[b * _width + l] |= Protected;
                        }
                        if (r < _width && b >= 0)
                        {
                            _stencil[b * _width + r] |= Protected;
                        }
                        if (l >= 0 && t < _height)
                        {
                            _stencil[t * _width + l] |= Protected;
                        }
                        if (r < _width && t < _height)
                        {
                            _stencil[t * _width + r] |= Protected;
                        }
                    }
                }
                prevEdge = edge;
            }
        }
    }

    /// <summary>Flags as protected the texels whose non-median channels make an edge between them and a neighbour.</summary>
    private void ProtectEdges(float[] sdf)
    {
        // Horizontal texel pairs
        var radius = (float)(ProtectionRadiusTolerance * _transformation.UnprojectVector(new Vector2d(_transformation.MapDelta(1), 0)).Length);
        for (var y = 0; y < _height; ++y)
        {
            for (var x = 0; x < _width - 1; ++x)
            {
                var left = (y * _width + x) * 3;
                var right = left + 3;
                var lm = Median(sdf[left], sdf[left + 1], sdf[left + 2]);
                var rm = Median(sdf[right], sdf[right + 1], sdf[right + 2]);
                if (Math.Abs(lm - .5f) + Math.Abs(rm - .5f) < radius)
                {
                    var mask = EdgeBetweenTexels(sdf, left, right);
                    ProtectExtremeChannels(y * _width + x, sdf, left, lm, mask);
                    ProtectExtremeChannels(y * _width + x + 1, sdf, right, rm, mask);
                }
            }
        }
        // Vertical texel pairs
        radius = (float)(ProtectionRadiusTolerance * _transformation.UnprojectVector(new Vector2d(0, _transformation.MapDelta(1))).Length);
        for (var y = 0; y < _height - 1; ++y)
        {
            for (var x = 0; x < _width; ++x)
            {
                var bottom = (y * _width + x) * 3;
                var top = bottom + _width * 3;
                var bm = Median(sdf[bottom], sdf[bottom + 1], sdf[bottom + 2]);
                var tm = Median(sdf[top], sdf[top + 1], sdf[top + 2]);
                if (Math.Abs(bm - .5f) + Math.Abs(tm - .5f) < radius)
                {
                    var mask = EdgeBetweenTexels(sdf, bottom, top);
                    ProtectExtremeChannels(y * _width + x, sdf, bottom, bm, mask);
                    ProtectExtremeChannels((y + 1) * _width + x, sdf, top, tm, mask);
                }
            }
        }
        // Diagonal texel pairs
        var delta = _transformation.MapDelta(1);
        radius = (float)(ProtectionRadiusTolerance * _transformation.UnprojectVector(new Vector2d(delta, delta)).Length);
        for (var y = 0; y < _height - 1; ++y)
        {
            for (var x = 0; x < _width - 1; ++x)
            {
                var lb = (y * _width + x) * 3;
                var rb = lb + 3;
                var lt = lb + _width * 3;
                var rt = lt + 3;
                var mlb = Median(sdf[lb], sdf[lb + 1], sdf[lb + 2]);
                var mrb = Median(sdf[rb], sdf[rb + 1], sdf[rb + 2]);
                var mlt = Median(sdf[lt], sdf[lt + 1], sdf[lt + 2]);
                var mrt = Median(sdf[rt], sdf[rt + 1], sdf[rt + 2]);
                if (Math.Abs(mlb - .5f) + Math.Abs(mrt - .5f) < radius)
                {
                    var mask = EdgeBetweenTexels(sdf, lb, rt);
                    ProtectExtremeChannels(y * _width + x, sdf, lb, mlb, mask);
                    ProtectExtremeChannels((y + 1) * _width + x + 1, sdf, rt, mrt, mask);
                }
                if (Math.Abs(mrb - .5f) + Math.Abs(mlt - .5f) < radius)
                {
                    var mask = EdgeBetweenTexels(sdf, rb, lt);
                    ProtectExtremeChannels(y * _width + x + 1, sdf, rb, mrb, mask);
                    ProtectExtremeChannels((y + 1) * _width + x, sdf, lt, mlt, mask);
                }
            }
        }
    }

    private void ProtectAll()
    {
        for (var i = 0; i < _stencil.Length; i++)
        {
            _stencil[i] |= Protected;
        }
    }

    /// <summary>Flags texels expected to cause interpolation artifacts, judging by the field alone.</summary>
    private void FindErrors(float[] sdf)
    {
        // Compute the expected deltas between values of horizontally, vertically, and diagonally adjacent texels.
        var (hSpan, vSpan, dSpan) = Spans();
        // Inspect all texels.
        for (var y = 0; y < _height; ++y)
        {
            for (var x = 0; x < _width; ++x)
            {
                var c = (y * _width + x) * 3;
                var cm = Median(sdf[c], sdf[c + 1], sdf[c + 2]);
                var protectedFlag = (_stencil[y * _width + x] & Protected) != 0;
                var h = new BaseArtifactClassifier(hSpan, protectedFlag);
                var v = new BaseArtifactClassifier(vSpan, protectedFlag);
                var d = new BaseArtifactClassifier(dSpan, protectedFlag);
                if (HasArtifact(sdf, x, y, c, cm, h, h, v, v, d, d, d, d))
                {
                    _stencil[y * _width + x] |= Error;
                }
            }
        }
    }

    /// <summary>
    /// Flags texels expected to cause interpolation artifacts, comparing with the exact distance
    /// to the shape. Visits texels in a serpentine order, which the distance finder's cache favours.
    /// </summary>
    private void FindErrors(float[] sdf, Shape shape)
    {
        // Compute the expected deltas between values of horizontally, vertically, and diagonally adjacent texels.
        var (hSpan, vSpan, dSpan) = Spans();
        var checker = new ShapeDistanceChecker(sdf, _width, _height, shape, _transformation, DefaultMinImproveRatio);
        var xDirection = 1;
        // Inspect all texels.
        for (var y = 0; y < _height; ++y)
        {
            var x = xDirection < 0 ? _width - 1 : 0;
            for (var col = 0; col < _width; ++col, x += xDirection)
            {
                if ((_stencil[y * _width + x] & Error) != 0)
                {
                    continue;
                }
                var c = (y * _width + x) * 3;
                checker.ShapeCoord = _transformation.Unproject(new Vector2d(x + .5, y + .5));
                checker.SdfCoord = new Vector2d(x + .5, y + .5);
                checker.Msd = c;
                checker.ProtectedFlag = (_stencil[y * _width + x] & Protected) != 0;
                var cm = Median(sdf[c], sdf[c + 1], sdf[c + 2]);
                // Mark current texel c with the error flag if an artifact occurs when it's interpolated with any of its 8 neighbors.
                if (HasArtifact(
                    sdf, x, y, c, cm,
                    checker.Classifier(new Vector2d(-1, 0), hSpan),
                    checker.Classifier(new Vector2d(+1, 0), hSpan),
                    checker.Classifier(new Vector2d(0, -1), vSpan),
                    checker.Classifier(new Vector2d(0, +1), vSpan),
                    checker.Classifier(new Vector2d(-1, -1), dSpan),
                    checker.Classifier(new Vector2d(+1, -1), dSpan),
                    checker.Classifier(new Vector2d(-1, +1), dSpan),
                    checker.Classifier(new Vector2d(+1, +1), dSpan)))
                {
                    _stencil[y * _width + x] |= Error;
                }
            }
            xDirection = -xDirection;
        }
    }

    /// <summary>Sets every channel of each flagged texel to its median.</summary>
    private void ApplyStencil(float[] sdf)
    {
        for (var i = 0; i < _stencil.Length; i++)
        {
            if ((_stencil[i] & Error) != 0)
            {
                var c = i * 3;
                var m = Median(sdf[c], sdf[c + 1], sdf[c + 2]);
                sdf[c] = m;
                sdf[c + 1] = m;
                sdf[c + 2] = m;
            }
        }
    }

    private (double H, double V, double D) Spans()
    {
        var delta = _transformation.MapDelta(1);
        return (
            DefaultMinDeviationRatio * _transformation.UnprojectVector(new Vector2d(delta, 0)).Length,
            DefaultMinDeviationRatio * _transformation.UnprojectVector(new Vector2d(0, delta)).Length,
            DefaultMinDeviationRatio * _transformation.UnprojectVector(new Vector2d(delta, delta)).Length);
    }

    /// <summary>
    /// Whether texel <paramref name="c"/> causes an artifact when interpolated with any of its 8
    /// neighbours, in msdfgen's order: left, below, right, above, then the diagonals.
    /// </summary>
    private bool HasArtifact<TClassifier>(
        float[] sdf, int x, int y, int c, float cm,
        TClassifier left, TClassifier right, TClassifier below, TClassifier above,
        TClassifier leftBelow, TClassifier rightBelow, TClassifier leftAbove, TClassifier rightAbove)
        where TClassifier : struct, IArtifactClassifier
    {
        var rowStride = _width * 3;
        int l = c - 3, b = c - rowStride, r = c + 3, t = c + rowStride;
        return
            (x > 0 && HasLinearArtifact(left, cm, sdf, c, l)) ||
            (y > 0 && HasLinearArtifact(below, cm, sdf, c, b)) ||
            (x < _width - 1 && HasLinearArtifact(right, cm, sdf, c, r)) ||
            (y < _height - 1 && HasLinearArtifact(above, cm, sdf, c, t)) ||
            (x > 0 && y > 0 && HasDiagonalArtifact(leftBelow, cm, sdf, c, l, b, b - 3)) ||
            (x < _width - 1 && y > 0 && HasDiagonalArtifact(rightBelow, cm, sdf, c, r, b, b + 3)) ||
            (x > 0 && y < _height - 1 && HasDiagonalArtifact(leftAbove, cm, sdf, c, l, t, t - 3)) ||
            (x < _width - 1 && y < _height - 1 && HasDiagonalArtifact(rightAbove, cm, sdf, c, r, t, t + 3));
    }

    /// <summary>Determines if the channel contributes to an edge between the two texels a, b.</summary>
    private static bool EdgeBetweenTexelsChannel(float[] sdf, int a, int b, int channel)
    {
        // Find interpolation ratio t (0 < t < 1) where an edge is expected (mix(a[channel], b[channel], t) == 0.5).
        var t = (sdf[a + channel] - .5) / (sdf[a + channel] - sdf[b + channel]);
        if (t > 0 && t < 1)
        {
            // Interpolate channel values at t.
            Span<float> c = [Mix(sdf[a], sdf[b], t), Mix(sdf[a + 1], sdf[b + 1], t), Mix(sdf[a + 2], sdf[b + 2], t)];
            // This is only an edge if the zero-distance channel is the median.
            return Median(c[0], c[1], c[2]) == c[channel];
        }
        return false;
    }

    /// <summary>Returns a bit mask of which channels contribute to an edge between the two texels a, b.</summary>
    private static EdgeColor EdgeBetweenTexels(float[] sdf, int a, int b) =>
        (EdgeBetweenTexelsChannel(sdf, a, b, 0) ? EdgeColor.Red : EdgeColor.Black) |
        (EdgeBetweenTexelsChannel(sdf, a, b, 1) ? EdgeColor.Green : EdgeColor.Black) |
        (EdgeBetweenTexelsChannel(sdf, a, b, 2) ? EdgeColor.Blue : EdgeColor.Black);

    /// <summary>Marks texel as protected if one of its non-median channels is present in the channel mask.</summary>
    private void ProtectExtremeChannels(int texel, float[] sdf, int msd, float m, EdgeColor mask)
    {
        if (
            ((mask & EdgeColor.Red) != 0 && sdf[msd] != m) ||
            ((mask & EdgeColor.Green) != 0 && sdf[msd + 1] != m) ||
            ((mask & EdgeColor.Blue) != 0 && sdf[msd + 2] != m))
        {
            _stencil[texel] |= Protected;
        }
    }

    /// <summary>Returns the median of the linear interpolation of texels a, b at t.</summary>
    private static float InterpolatedMedian(float[] sdf, int a, int b, double t) =>
        Median(Mix(sdf[a], sdf[b], t), Mix(sdf[a + 1], sdf[b + 1], t), Mix(sdf[a + 2], sdf[b + 2], t));

    /// <summary>Returns the median of the bilinear interpolation with the given constant, linear, and quadratic terms at t.</summary>
    private static float InterpolatedMedian(float[] sdf, int a, ReadOnlySpan<float> l, ReadOnlySpan<float> q, double t)
    {
        var c0 = t * (t * q[0] + l[0]) + sdf[a];
        var c1 = t * (t * q[1] + l[1]) + sdf[a + 1];
        var c2 = t * (t * q[2] + l[2]) + sdf[a + 2];
        return (float)Math.Max(Math.Min(c0, c1), Math.Min(Math.Max(c0, c1), c2));
    }

    /// <summary>Checks if a linear interpolation artifact will occur at a point where two specific color channels are equal - such points have extreme median values.</summary>
    private static bool HasLinearArtifactInner<TClassifier>(TClassifier classifier, float am, float bm, float[] sdf, int a, int b, float dA, float dB)
        where TClassifier : struct, IArtifactClassifier
    {
        // Find interpolation ratio t (0 < t < 1) where two color channels are equal (mix(dA, dB, t) == 0).
        var t = (double)dA / (dA - dB);
        if (t > ArtifactTEpsilon && t < 1 - ArtifactTEpsilon)
        {
            // Interpolate median at t and let the classifier decide if its value indicates an artifact.
            var xm = InterpolatedMedian(sdf, a, b, t);
            return classifier.Evaluate(t, xm, classifier.RangeTest(0, 1, t, am, bm, xm));
        }
        return false;
    }

    /// <summary>Checks if a bilinear interpolation artifact will occur at a point where two specific color channels are equal - such points have extreme median values.</summary>
    private static bool HasDiagonalArtifactInner<TClassifier>(
        TClassifier classifier, float am, float dm, float[] sdf, int a, ReadOnlySpan<float> l, ReadOnlySpan<float> q,
        float dA, float dBC, float dD, double tEx0, double tEx1)
        where TClassifier : struct, IArtifactClassifier
    {
        // Find interpolation ratios t (0 < t[i] < 1) where two color channels are equal.
        Span<double> t = stackalloc double[2];
        var solutions = EquationSolver.SolveQuadratic(t, dD - dBC + dA, dBC - dA - dA, dA);
        Span<double> tEnd = stackalloc double[2];
        Span<float> em = stackalloc float[2];
        for (var i = 0; i < solutions; ++i)
        {
            // Solutions t[i] == 0 and t[i] == 1 are singularities and occur very often because two channels are usually equal at texels.
            if (t[i] > ArtifactTEpsilon && t[i] < 1 - ArtifactTEpsilon)
            {
                // Interpolate median xm at t.
                var xm = InterpolatedMedian(sdf, a, l, q, t[i]);
                // Determine if xm deviates too much from medians of a, d.
                var rangeFlags = classifier.RangeTest(0, 1, t[i], am, dm, xm);
                // Additionally, check xm against the interpolated medians at the local extremes tEx0, tEx1.
                // tEx0
                if (tEx0 > 0 && tEx0 < 1)
                {
                    tEnd[0] = 0;
                    tEnd[1] = 1;
                    em[0] = am;
                    em[1] = dm;
                    var side = tEx0 > t[i] ? 1 : 0;
                    tEnd[side] = tEx0;
                    em[side] = InterpolatedMedian(sdf, a, l, q, tEx0);
                    rangeFlags |= classifier.RangeTest(tEnd[0], tEnd[1], t[i], em[0], em[1], xm);
                }
                // tEx1
                if (tEx1 > 0 && tEx1 < 1)
                {
                    tEnd[0] = 0;
                    tEnd[1] = 1;
                    em[0] = am;
                    em[1] = dm;
                    var side = tEx1 > t[i] ? 1 : 0;
                    tEnd[side] = tEx1;
                    em[side] = InterpolatedMedian(sdf, a, l, q, tEx1);
                    rangeFlags |= classifier.RangeTest(tEnd[0], tEnd[1], t[i], em[0], em[1], xm);
                }
                if (classifier.Evaluate(t[i], xm, rangeFlags))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Checks if a linear interpolation artifact will occur inbetween two horizontally or vertically adjacent texels a, b.</summary>
    private static bool HasLinearArtifact<TClassifier>(TClassifier classifier, float am, float[] sdf, int a, int b)
        where TClassifier : struct, IArtifactClassifier
    {
        var bm = Median(sdf[b], sdf[b + 1], sdf[b + 2]);
        return
            // Out of the pair, only report artifacts for the texel further from the edge to minimize side effects.
            Math.Abs(am - .5f) >= Math.Abs(bm - .5f) && (
                // Check points where each pair of color channels meets.
                HasLinearArtifactInner(classifier, am, bm, sdf, a, b, sdf[a + 1] - sdf[a], sdf[b + 1] - sdf[b]) ||
                HasLinearArtifactInner(classifier, am, bm, sdf, a, b, sdf[a + 2] - sdf[a + 1], sdf[b + 2] - sdf[b + 1]) ||
                HasLinearArtifactInner(classifier, am, bm, sdf, a, b, sdf[a] - sdf[a + 2], sdf[b] - sdf[b + 2]));
    }

    /// <summary>Checks if a bilinear interpolation artifact will occur inbetween two diagonally adjacent texels a, d (with b, c forming the other diagonal).</summary>
    private static bool HasDiagonalArtifact<TClassifier>(TClassifier classifier, float am, float[] sdf, int a, int b, int c, int d)
        where TClassifier : struct, IArtifactClassifier
    {
        var dm = Median(sdf[d], sdf[d + 1], sdf[d + 2]);
        // Out of the pair, only report artifacts for the texel further from the edge to minimize side effects.
        if (Math.Abs(am - .5f) < Math.Abs(dm - .5f))
        {
            return false;
        }
        Span<float> abc = [sdf[a] - sdf[b] - sdf[c], sdf[a + 1] - sdf[b + 1] - sdf[c + 1], sdf[a + 2] - sdf[b + 2] - sdf[c + 2]];
        // Compute the linear terms for bilinear interpolation.
        Span<float> l = [-sdf[a] - abc[0], -sdf[a + 1] - abc[1], -sdf[a + 2] - abc[2]];
        // Compute the quadratic terms for bilinear interpolation.
        Span<float> q = [sdf[d] + abc[0], sdf[d + 1] + abc[1], sdf[d + 2] + abc[2]];
        // Compute interpolation ratios tEx (0 < tEx[i] < 1) for the local extremes of each color channel (the derivative 2*q[i]*tEx[i]+l[i] == 0).
        Span<double> tEx = [-.5 * l[0] / q[0], -.5 * l[1] / q[1], -.5 * l[2] / q[2]];
        // Check points where each pair of color channels meets.
        return
            HasDiagonalArtifactInner(classifier, am, dm, sdf, a, l, q, sdf[a + 1] - sdf[a], sdf[b + 1] - sdf[b] + sdf[c + 1] - sdf[c], sdf[d + 1] - sdf[d], tEx[0], tEx[1]) ||
            HasDiagonalArtifactInner(classifier, am, dm, sdf, a, l, q, sdf[a + 2] - sdf[a + 1], sdf[b + 2] - sdf[b + 1] + sdf[c + 2] - sdf[c + 1], sdf[d + 2] - sdf[d + 1], tEx[1], tEx[2]) ||
            HasDiagonalArtifactInner(classifier, am, dm, sdf, a, l, q, sdf[a] - sdf[a + 2], sdf[b] - sdf[b + 2] + sdf[c] - sdf[c + 2], sdf[d] - sdf[d + 2], tEx[2], tEx[0]);
    }
}
