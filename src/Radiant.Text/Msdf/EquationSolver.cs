// Ported from msdfgen (https://github.com/Chlumsky/msdfgen) at commit 1c106ed8, core/equation-solver.cpp.
// Copyright (c) 2014 - 2025 Viktor Chlumsky, licensed under the MIT License (see THIRD-PARTY-NOTICES.md).

using System;

namespace Radiant.Text.Msdf;

/// <summary>
/// Real roots of quadratics and cubics, for the nearest point on a quadratic Bézier and the error
/// correction's search for where channels cross. Nearly degenerate equations are solved as the
/// lower degree, where that is numerically safer.
/// </summary>
internal static class EquationSolver
{
    /// <summary>Solves a x² + b x + c = 0 into <paramref name="x"/>: the number of roots, or -1 if every x is one.</summary>
    public static int SolveQuadratic(Span<double> x, double a, double b, double c)
    {
        // a == 0 -> linear equation
        if (a == 0 || Math.Abs(b) > 1e12 * Math.Abs(a))
        {
            // a == 0, b == 0 -> no solution
            if (b == 0)
            {
                return c == 0 ? -1 : 0; // 0 == 0
            }
            x[0] = -c / b;
            return 1;
        }
        var dscr = b * b - 4 * a * c;
        if (dscr > 0)
        {
            dscr = Math.Sqrt(dscr);
            x[0] = (-b + dscr) / (2 * a);
            x[1] = (-b - dscr) / (2 * a);
            return 2;
        }
        if (dscr == 0)
        {
            x[0] = -b / (2 * a);
            return 1;
        }
        return 0;
    }

    /// <summary>Solves a x³ + b x² + c x + d = 0 into <paramref name="x"/>: the number of roots, or -1 if every x is one.</summary>
    public static int SolveCubic(Span<double> x, double a, double b, double c, double d)
    {
        if (a != 0)
        {
            var bn = b / a;
            if (Math.Abs(bn) < 1e6) // Above this ratio, the numerical error gets larger than if we treated a as zero
            {
                return SolveCubicNormed(x, bn, c / a, d / a);
            }
        }
        return SolveQuadratic(x, b, c, d);
    }

    private static int SolveCubicNormed(Span<double> x, double a, double b, double c)
    {
        var a2 = a * a;
        var q = 1 / 9.0 * (a2 - 3 * b);
        var r = 1 / 54.0 * (a * (2 * a2 - 9 * b) + 27 * c);
        var r2 = r * r;
        var q3 = q * q * q;
        a *= 1 / 3.0;
        if (r2 < q3)
        {
            var t = r / Math.Sqrt(q3);
            if (t < -1)
            {
                t = -1;
            }
            if (t > 1)
            {
                t = 1;
            }
            t = Math.Acos(t);
            q = -2 * Math.Sqrt(q);
            x[0] = q * Math.Cos(1 / 3.0 * t) - a;
            x[1] = q * Math.Cos(1 / 3.0 * (t + 2 * Math.PI)) - a;
            x[2] = q * Math.Cos(1 / 3.0 * (t - 2 * Math.PI)) - a;
            return 3;
        }
        var u = (r < 0 ? 1 : -1) * Math.Pow(Math.Abs(r) + Math.Sqrt(r2 - q3), 1 / 3.0);
        var v = u == 0 ? 0 : q / u;
        x[0] = u + v - a;
        if (u == v || Math.Abs(u - v) < 1e-12 * Math.Abs(u + v))
        {
            x[1] = -.5 * (u + v) - a;
            return 2;
        }
        return 1;
    }
}
