// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// CAM16, a color appearance model. Colors are not just defined by their hex code, but rather, a
/// hex code and viewing conditions.
/// <para>
/// CAM16 instances also have coordinates in the CAM16-UCS space, <see cref="JStar"/>,
/// <see cref="AStar"/> and <see cref="BStar"/>. Euclidean distance there (<see cref="Distance"/>)
/// is perceptual color difference.
/// </para>
/// </summary>
public sealed class Cam16
{
    private Cam16(double hue, double chroma, double j, double q, double m, double s, double jstar, double astar, double bstar)
    {
        Hue = hue;
        Chroma = chroma;
        J = j;
        Q = q;
        M = m;
        S = s;
        JStar = jstar;
        AStar = astar;
        BStar = bstar;
    }

    /// <summary>Hue in CAM16.</summary>
    public double Hue { get; }

    /// <summary>Chroma in CAM16.</summary>
    public double Chroma { get; }

    /// <summary>Lightness in CAM16.</summary>
    public double J { get; }

    /// <summary>Brightness in CAM16: lightness adjusted for the viewing conditions.</summary>
    public double Q { get; }

    /// <summary>Colorfulness in CAM16: chroma adjusted for the viewing conditions.</summary>
    public double M { get; }

    /// <summary>Saturation in CAM16: colorfulness relative to brightness.</summary>
    public double S { get; }

    /// <summary>Lightness coordinate in CAM16-UCS.</summary>
    public double JStar { get; }

    /// <summary>a* coordinate in CAM16-UCS.</summary>
    public double AStar { get; }

    /// <summary>b* coordinate in CAM16-UCS.</summary>
    public double BStar { get; }

    /// <summary>CAM16 instances also have coordinates in CAM16-UCS; this is their perceptual distance.</summary>
    public double Distance(Cam16 other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var dJ = JStar - other.JStar;
        var dA = AStar - other.AStar;
        var dB = BStar - other.BStar;
        var dEPrime = Math.Sqrt(dJ * dJ + dA * dA + dB * dB);
        var dE = 1.41 * Math.Pow(dEPrime, 0.63);
        return dE;
    }

    /// <summary>A color in default viewing conditions.</summary>
    public static Cam16 FromInt(int argb) => FromIntInViewingConditions(argb, ViewingConditions.Default);

    /// <summary>A color in the given viewing conditions.</summary>
    public static Cam16 FromIntInViewingConditions(int argb, ViewingConditions viewingConditions)
    {
        ArgumentNullException.ThrowIfNull(viewingConditions);
        var red = (argb & 0x00ff0000) >> 16;
        var green = (argb & 0x0000ff00) >> 8;
        var blue = argb & 0x000000ff;
        var redL = ColorUtils.Linearized(red);
        var greenL = ColorUtils.Linearized(green);
        var blueL = ColorUtils.Linearized(blue);
        var x = 0.41233895 * redL + 0.35762064 * greenL + 0.18051042 * blueL;
        var y = 0.2126 * redL + 0.7152 * greenL + 0.0722 * blueL;
        var z = 0.01932141 * redL + 0.11916382 * greenL + 0.95034478 * blueL;

        var rC = 0.401288 * x + 0.650173 * y - 0.051461 * z;
        var gC = -0.250268 * x + 1.204414 * y + 0.045854 * z;
        var bC = -0.002079 * x + 0.048952 * y + 0.953127 * z;

        var rD = viewingConditions.RgbD0 * rC;
        var gD = viewingConditions.RgbD1 * gC;
        var bD = viewingConditions.RgbD2 * bC;

        var rAF = Math.Pow(viewingConditions.Fl * Math.Abs(rD) / 100.0, 0.42);
        var gAF = Math.Pow(viewingConditions.Fl * Math.Abs(gD) / 100.0, 0.42);
        var bAF = Math.Pow(viewingConditions.Fl * Math.Abs(bD) / 100.0, 0.42);

        var rA = MathUtils.Signum(rD) * 400.0 * rAF / (rAF + 27.13);
        var gA = MathUtils.Signum(gD) * 400.0 * gAF / (gAF + 27.13);
        var bA = MathUtils.Signum(bD) * 400.0 * bAF / (bAF + 27.13);

        var a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        var b = (rA + gA - 2.0 * bA) / 9.0;
        var u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
        var p2 = (40.0 * rA + 20.0 * gA + bA) / 20.0;
        var atan2 = Math.Atan2(b, a);
        var atanDegrees = atan2 * 180.0 / Math.PI;
        var hue = MathUtils.SanitizeDegreesDouble(atanDegrees);
        var hueRadians = hue * Math.PI / 180.0;

        var ac = p2 * viewingConditions.Nbb;
        var j = 100.0 * Math.Pow(ac / viewingConditions.Aw, viewingConditions.C * viewingConditions.Z);
        var q = 4.0 / viewingConditions.C * Math.Sqrt(j / 100.0) * (viewingConditions.Aw + 4.0) * viewingConditions.FlRoot;
        var huePrime = hue < 20.14 ? hue + 360 : hue;
        var eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);
        var p1 = 50000.0 / 13.0 * eHue * viewingConditions.Nc * viewingConditions.Ncb;
        var t = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
        var alpha = Math.Pow(t, 0.9) * Math.Pow(1.64 - Math.Pow(0.29, viewingConditions.N), 0.73);
        var c = alpha * Math.Sqrt(j / 100.0);
        var m = c * viewingConditions.FlRoot;
        var s = 50.0 * Math.Sqrt(alpha * viewingConditions.C / (viewingConditions.Aw + 4.0));
        var jstar = (1.0 + 100.0 * 0.007) * j / (1.0 + 0.007 * j);
        var mstar = 1.0 / 0.0228 * Math.Log(1.0 + 0.0228 * m);
        var astar = mstar * Math.Cos(hueRadians);
        var bstar = mstar * Math.Sin(hueRadians);

        return new Cam16(hue, c, j, q, m, s, jstar, astar, bstar);
    }

    /// <summary>A color from CAM16 lightness, chroma and hue in default viewing conditions.</summary>
    public static Cam16 FromJch(double j, double c, double h) =>
        FromJchInViewingConditions(j, c, h, ViewingConditions.Default);

    /// <summary>A color from CAM16 lightness, chroma and hue in the given viewing conditions.</summary>
    public static Cam16 FromJchInViewingConditions(double j, double c, double h, ViewingConditions viewingConditions)
    {
        ArgumentNullException.ThrowIfNull(viewingConditions);
        var q = 4.0 / viewingConditions.C * Math.Sqrt(j / 100.0) * (viewingConditions.Aw + 4.0) * viewingConditions.FlRoot;
        var m = c * viewingConditions.FlRoot;
        var alpha = c / Math.Sqrt(j / 100.0);
        var s = 50.0 * Math.Sqrt(alpha * viewingConditions.C / (viewingConditions.Aw + 4.0));
        var hueRadians = h * Math.PI / 180.0;
        var jstar = (1.0 + 100.0 * 0.007) * j / (1.0 + 0.007 * j);
        var mstar = 1.0 / 0.0228 * Math.Log(1.0 + 0.0228 * m);
        var astar = mstar * Math.Cos(hueRadians);
        var bstar = mstar * Math.Sin(hueRadians);
        return new Cam16(h, c, j, q, m, s, jstar, astar, bstar);
    }

    /// <summary>A color from CAM16-UCS coordinates in default viewing conditions.</summary>
    public static Cam16 FromUcs(double jstar, double astar, double bstar) =>
        FromUcsInViewingConditions(jstar, astar, bstar, ViewingConditions.Default);

    /// <summary>A color from CAM16-UCS coordinates in the given viewing conditions.</summary>
    public static Cam16 FromUcsInViewingConditions(double jstar, double astar, double bstar, ViewingConditions viewingConditions)
    {
        var a = astar;
        var b = bstar;
        var m = Math.Sqrt(a * a + b * b);
        var bigM = (Math.Exp(m * 0.0228) - 1.0) / 0.0228;
        var c = bigM / viewingConditions.FlRoot;
        var h = Math.Atan2(b, a) * (180.0 / Math.PI);
        if (h < 0.0)
        {
            h += 360.0;
        }
        var j = jstar / (1 - (jstar - 100) * 0.007);
        return FromJchInViewingConditions(j, c, h, viewingConditions);
    }

    /// <summary>ARGB representation of this color, assuming default viewing conditions.</summary>
    public int ToInt() => Viewed(ViewingConditions.Default);

    /// <summary>ARGB representation of this color as seen in <paramref name="viewingConditions"/>.</summary>
    public int Viewed(ViewingConditions viewingConditions)
    {
        var xyz = XyzInViewingConditions(viewingConditions);
        return ColorUtils.ArgbFromXyz(xyz[0], xyz[1], xyz[2]);
    }

    /// <summary>A color expressed in XYZ and viewed in <paramref name="viewingConditions"/>, as CAM16.</summary>
    public static Cam16 FromXyzInViewingConditions(double x, double y, double z, ViewingConditions viewingConditions)
    {
        ArgumentNullException.ThrowIfNull(viewingConditions);
        // Transform XYZ to 'cone'/'rgb' responses
        var rC = 0.401288 * x + 0.650173 * y - 0.051461 * z;
        var gC = -0.250268 * x + 1.204414 * y + 0.045854 * z;
        var bC = -0.002079 * x + 0.048952 * y + 0.953127 * z;

        // Discount illuminant
        var rD = viewingConditions.RgbD0 * rC;
        var gD = viewingConditions.RgbD1 * gC;
        var bD = viewingConditions.RgbD2 * bC;

        // Chromatic adaptation
        var rAF = Math.Pow(viewingConditions.Fl * Math.Abs(rD) / 100.0, 0.42);
        var gAF = Math.Pow(viewingConditions.Fl * Math.Abs(gD) / 100.0, 0.42);
        var bAF = Math.Pow(viewingConditions.Fl * Math.Abs(bD) / 100.0, 0.42);
        var rA = MathUtils.Signum(rD) * 400.0 * rAF / (rAF + 27.13);
        var gA = MathUtils.Signum(gD) * 400.0 * gAF / (gAF + 27.13);
        var bA = MathUtils.Signum(bD) * 400.0 * bAF / (bAF + 27.13);

        // Redness-greenness
        var a = (11.0 * rA + -12.0 * gA + bA) / 11.0;
        // Yellowness-blueness
        var b = (rA + gA - 2.0 * bA) / 9.0;

        // Auxiliary components
        var u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
        var p2 = (40.0 * rA + 20.0 * gA + bA) / 20.0;

        // Hue
        var atan2 = Math.Atan2(b, a);
        var atanDegrees = atan2 * 180.0 / Math.PI;
        var hue = atanDegrees < 0 ? atanDegrees + 360.0
            : atanDegrees >= 360 ? atanDegrees - 360
            : atanDegrees;
        var hueRadians = hue * Math.PI / 180.0;

        // Achromatic response to color
        var ac = p2 * viewingConditions.Nbb;

        // CAM16 lightness and brightness
        var bigJ = 100.0 * Math.Pow(ac / viewingConditions.Aw, viewingConditions.C * viewingConditions.Z);
        var bigQ = 4.0 / viewingConditions.C * Math.Sqrt(bigJ / 100.0) * (viewingConditions.Aw + 4.0) * viewingConditions.FlRoot;

        var huePrime = hue < 20.14 ? hue + 360 : hue;
        var eHue = 1.0 / 4.0 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);
        var p1 = 50000.0 / 13.0 * eHue * viewingConditions.Nc * viewingConditions.Ncb;
        var t = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
        var alpha = Math.Pow(t, 0.9) * Math.Pow(1.64 - Math.Pow(0.29, viewingConditions.N), 0.73);
        // CAM16 chroma, colorfulness, saturation
        var bigC = alpha * Math.Sqrt(bigJ / 100.0);
        var bigM = bigC * viewingConditions.FlRoot;
        var s = 50.0 * Math.Sqrt(alpha * viewingConditions.C / (viewingConditions.Aw + 4.0));

        // CAM16-UCS components
        var jstar = (1.0 + 100.0 * 0.007) * bigJ / (1.0 + 0.007 * bigJ);
        var mstar = Math.Log(1.0 + 0.0228 * bigM) / 0.0228;
        var astar = mstar * Math.Cos(hueRadians);
        var bstar = mstar * Math.Sin(hueRadians);
        return new Cam16(hue, bigC, bigJ, bigQ, bigM, s, jstar, astar, bstar);
    }

    /// <summary>XYZ representation of this color seen in <paramref name="viewingConditions"/>.</summary>
    public double[] XyzInViewingConditions(ViewingConditions viewingConditions)
    {
        ArgumentNullException.ThrowIfNull(viewingConditions);
        var alpha = Chroma == 0.0 || J == 0.0 ? 0.0 : Chroma / Math.Sqrt(J / 100.0);

        var t = Math.Pow(alpha / Math.Pow(1.64 - Math.Pow(0.29, viewingConditions.N), 0.73), 1.0 / 0.9);
        var hRad = Hue * Math.PI / 180.0;

        var eHue = 0.25 * (Math.Cos(hRad + 2.0) + 3.8);
        var ac = viewingConditions.Aw * Math.Pow(J / 100.0, 1.0 / viewingConditions.C / viewingConditions.Z);
        var p1 = eHue * (50000.0 / 13.0) * viewingConditions.Nc * viewingConditions.Ncb;

        var p2 = ac / viewingConditions.Nbb;

        var hSin = Math.Sin(hRad);
        var hCos = Math.Cos(hRad);

        var gamma = 23.0 * (p2 + 0.305) * t / (23.0 * p1 + 11 * t * hCos + 108.0 * t * hSin);
        var a = gamma * hCos;
        var b = gamma * hSin;
        var rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
        var gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
        var bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;

        var rCBase = Math.Max(0, 27.13 * Math.Abs(rA) / (400.0 - Math.Abs(rA)));
        var rC = MathUtils.Signum(rA) * (100.0 / viewingConditions.Fl) * Math.Pow(rCBase, 1.0 / 0.42);
        var gCBase = Math.Max(0, 27.13 * Math.Abs(gA) / (400.0 - Math.Abs(gA)));
        var gC = MathUtils.Signum(gA) * (100.0 / viewingConditions.Fl) * Math.Pow(gCBase, 1.0 / 0.42);
        var bCBase = Math.Max(0, 27.13 * Math.Abs(bA) / (400.0 - Math.Abs(bA)));
        var bC = MathUtils.Signum(bA) * (100.0 / viewingConditions.Fl) * Math.Pow(bCBase, 1.0 / 0.42);
        var rF = rC / viewingConditions.RgbD0;
        var gF = gC / viewingConditions.RgbD1;
        var bF = bC / viewingConditions.RgbD2;

        var x = 1.86206786 * rF - 1.01125463 * gF + 0.14918677 * bF;
        var y = 0.38752654 * rF + 0.62144744 * gF - 0.00897398 * bF;
        var z = -0.01584150 * rF - 0.03412294 * gF + 1.04996444 * bF;

        return [x, y, z];
    }
}
