using System;
using System.Numerics;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Helpers shared across edge types. Uses Vector2 (single precision) for
    /// data storage but does the numerically sensitive bits in double.
    /// </summary>
    public static class Geometry
    {
        public static Vector2 Mix(Vector2 a, Vector2 b, double t)
            => new((float)((1 - t) * a.X + t * b.X), (float)((1 - t) * a.Y + t * b.Y));

        public static double Cross(Vector2 a, Vector2 b)
            => (double)a.X * b.Y - (double)a.Y * b.X;

        public static double DotD(Vector2 a, Vector2 b)
            => (double)a.X * b.X + (double)a.Y * b.Y;

        public static double LengthD(Vector2 v)
            => Math.Sqrt((double)v.X * v.X + (double)v.Y * v.Y);

        public static Vector2 NormalizeSafe(Vector2 v)
        {
            var len = LengthD(v);
            if (len < 1e-20) return Vector2.Zero;
            return new Vector2((float)(v.X / len), (float)(v.Y / len));
        }

        public static double NonZeroSign(double n) => n > 0 ? 1.0 : -1.0;

        public static int SolveQuadratic(Span<double> roots, double a, double b, double c)
        {
            if (Math.Abs(a) < 1e-14)
            {
                if (Math.Abs(b) < 1e-14) return c == 0 ? -1 : 0;
                roots[0] = -c / b;
                return 1;
            }
            var disc = b * b - 4 * a * c;
            if (disc > 0)
            {
                disc = Math.Sqrt(disc);
                roots[0] = (-b + disc) / (2 * a);
                roots[1] = (-b - disc) / (2 * a);
                return 2;
            }
            if (disc == 0)
            {
                roots[0] = -b / (2 * a);
                return 1;
            }
            return 0;
        }

        public static int SolveCubic(Span<double> roots, double a, double b, double c, double d)
        {
            if (Math.Abs(a) < 1e-14) return SolveQuadratic(roots, b, c, d);
            return SolveCubicNormed(roots, b / a, c / a, d / a);
        }

        private static int SolveCubicNormed(Span<double> roots, double a, double b, double c)
        {
            var a2 = a * a;
            var q = (a2 - 3 * b) / 9;
            var r = (a * (2 * a2 - 9 * b) + 27 * c) / 54;
            var r2 = r * r;
            var q3 = q * q * q;
            double A, B;
            if (r2 < q3)
            {
                var t = r / Math.Sqrt(q3);
                if (t < -1) t = -1;
                if (t > 1) t = 1;
                t = Math.Acos(t);
                a /= 3;
                q = -2 * Math.Sqrt(q);
                roots[0] = q * Math.Cos(t / 3) - a;
                roots[1] = q * Math.Cos((t + 2 * Math.PI) / 3) - a;
                roots[2] = q * Math.Cos((t - 2 * Math.PI) / 3) - a;
                return 3;
            }
            A = -Math.Pow(Math.Abs(r) + Math.Sqrt(r2 - q3), 1.0 / 3);
            if (r < 0) A = -A;
            B = A == 0 ? 0 : q / A;
            a /= 3;
            roots[0] = (A + B) - a;
            roots[1] = -0.5 * (A + B) - a;
            var im = 0.5 * Math.Sqrt(3.0) * (A - B);
            if (Math.Abs(im) < 1e-14)
            {
                roots[2] = roots[1];
                return 3;
            }
            return 1;
        }
    }
}
