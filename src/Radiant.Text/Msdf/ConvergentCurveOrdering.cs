// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/convergent-curve-ordering.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Which side of the other each of two curves leaves a shared corner on, when they leave it in
/// exactly opposite directions (a cusp). The distance field cannot tell such curves apart at the
/// corner, so <see cref="Shape.Normalize"/> pushes them apart in the direction this gives.
/// <para>
/// For curves A(t), B(t) from the corner P, this is the limit as t → 0 of
/// sign(cross(A(t / |A'(0)|) − P, B(t / |B'(0)|) − P)), the parameters normed so both approach P
/// at the same rate. That is the sign of the lowest-order non-zero coefficient of the cross
/// product's polynomial. Curves whose first control point is the corner approach with √t instead.
/// </para>
/// </summary>
internal static class ConvergentCurveOrdering
{
    /// <summary>-1, 0 or 1: the order in which <paramref name="a"/> (ending at the corner) and <paramref name="b"/> (starting there) leave it.</summary>
    public static int Compute(EdgeSegment a, EdgeSegment b)
    {
        Span<Vector2d> controlPoints = stackalloc Vector2d[12];
        const int corner = 4;
        Span<Vector2d> aCpTmp = stackalloc Vector2d[4];
        var aOrder = a.Type;
        var bOrder = b.Type;
        if (!(aOrder >= 1 && aOrder <= 3 && bOrder >= 1 && bOrder <= 3))
        {
            // Not implemented - only linear, quadratic, and cubic curves supported
            return 0;
        }
        a.CopyControlPoints(aCpTmp);
        b.CopyControlPoints(controlPoints[corner..]);
        if (aCpTmp[aOrder] != controlPoints[corner])
        {
            return 0;
        }
        SimplifyDegenerateCurve(aCpTmp, ref aOrder);
        SimplifyDegenerateCurve(controlPoints[corner..], ref bOrder);
        for (var i = 0; i < aOrder; ++i)
        {
            controlPoints[corner + i - aOrder] = aCpTmp[i];
        }
        return Compute(controlPoints, corner, aOrder, bOrder);
    }

    private static void SimplifyDegenerateCurve(Span<Vector2d> controlPoints, ref int order)
    {
        if (order == 3 && (controlPoints[1] == controlPoints[0] || controlPoints[1] == controlPoints[3])
            && (controlPoints[2] == controlPoints[0] || controlPoints[2] == controlPoints[3]))
        {
            controlPoints[1] = controlPoints[3];
            order = 1;
        }
        if (order == 2 && (controlPoints[1] == controlPoints[0] || controlPoints[1] == controlPoints[2]))
        {
            controlPoints[1] = controlPoints[2];
            order = 1;
        }
        if (order == 1 && controlPoints[0] == controlPoints[1])
        {
            order = 0;
        }
    }

    private static int Compute(ReadOnlySpan<Vector2d> p, int corner, int controlPointsBefore, int controlPointsAfter)
    {
        if (!(controlPointsBefore > 0 && controlPointsAfter > 0))
        {
            return 0;
        }
        Vector2d a2 = default, a3 = default, b2 = default, b3 = default;
        var a1 = p[corner - 1] - p[corner];
        var b1 = p[corner + 1] - p[corner];
        if (controlPointsBefore >= 2)
        {
            a2 = p[corner - 2] - p[corner - 1] - a1;
        }
        if (controlPointsAfter >= 2)
        {
            b2 = p[corner + 2] - p[corner + 1] - b1;
        }
        if (controlPointsBefore >= 3)
        {
            a3 = p[corner - 3] - p[corner - 2] - (p[corner - 2] - p[corner - 1]) - a2;
            a2 = 3 * a2;
        }
        if (controlPointsAfter >= 3)
        {
            b3 = p[corner + 3] - p[corner + 2] - (p[corner + 2] - p[corner + 1]) - b2;
            b2 = 3 * b2;
        }
        a1 = controlPointsBefore * a1;
        b1 = controlPointsAfter * b1;
        double d;
        // Non-degenerate case
        if (a1.IsNonZero && b1.IsNonZero)
        {
            var @as = a1.Length;
            var bs = b1.Length;
            // Third derivative
            if ((d = @as * Vector2d.Cross(a1, b2) + bs * Vector2d.Cross(a2, b1)) != 0)
            {
                return Math.Sign(d);
            }
            // Fourth derivative
            if ((d = @as * @as * Vector2d.Cross(a1, b3) + @as * bs * Vector2d.Cross(a2, b2) + bs * bs * Vector2d.Cross(a3, b1)) != 0)
            {
                return Math.Sign(d);
            }
            // Fifth derivative
            if ((d = @as * Vector2d.Cross(a2, b3) + bs * Vector2d.Cross(a3, b2)) != 0)
            {
                return Math.Sign(d);
            }
            // Sixth derivative
            return Math.Sign(Vector2d.Cross(a3, b3));
        }
        // Degenerate curve after corner (control point after corner equals corner)
        var s = 1;
        if (a1.IsNonZero)
        {
            // !b1: swap aN <-> bN and handle in if (b1)
            b1 = a1;
            a1 = b2;
            b2 = a2;
            a2 = a1;
            a1 = b3;
            b3 = a3;
            a3 = a1;
            s = -1; // make sure to also flip output
        }
        // Degenerate curve before corner (control point before corner equals corner)
        if (b1.IsNonZero)
        {
            // !a1
            // Two-and-a-half-th derivative
            if ((d = Vector2d.Cross(a3, b1)) != 0)
            {
                return s * Math.Sign(d);
            }
            // Third derivative
            if ((d = Vector2d.Cross(a2, b2)) != 0)
            {
                return s * Math.Sign(d);
            }
            // Three-and-a-half-th derivative
            if ((d = Vector2d.Cross(a3, b2)) != 0)
            {
                return s * Math.Sign(d);
            }
            // Fourth derivative
            if ((d = Vector2d.Cross(a2, b3)) != 0)
            {
                return s * Math.Sign(d);
            }
            // Four-and-a-half-th derivative
            return s * Math.Sign(Vector2d.Cross(a3, b3));
        }
        // Degenerate curves on both sides of the corner (control point before and after corner equals corner)
        // Two-and-a-half-th derivative
        if ((d = Math.Sqrt(a2.Length) * Vector2d.Cross(a2, b3) + Math.Sqrt(b2.Length) * Vector2d.Cross(a3, b2)) != 0)
        {
            return Math.Sign(d);
        }
        // Third derivative
        return Math.Sign(Vector2d.Cross(a3, b3));
    }
}
