// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>
/// The environment a color is viewed in, which color appearance models such as CAM16 take into
/// account: white under a midday-sun white point measures as a slightly chromatic blue (roughly hue
/// 203, chroma 3, lightness 100).
/// <para>
/// Holds the intermediate values of the CAM16 conversion that depend only on the viewing
/// conditions. Their names are the colour-science shorthand; see Fairchild's <i>Color Appearance
/// Models</i> for what each means.
/// </para>
/// </summary>
public sealed class ViewingConditions
{
    /// <summary>sRGB-like viewing conditions.</summary>
    public static ViewingConditions Default { get; } = Make();

    private readonly double[] _rgbD;

    private ViewingConditions(
        double n, double aw, double nbb, double ncb, double c, double nc, double[] rgbD, double fl, double flRoot, double z)
    {
        N = n;
        Aw = aw;
        Nbb = nbb;
        Ncb = ncb;
        C = c;
        Nc = nc;
        _rgbD = rgbD;
        Fl = fl;
        FlRoot = flRoot;
        Z = z;
    }

    public double N { get; }
    public double Aw { get; }
    public double Nbb { get; }
    public double Ncb { get; }
    public double C { get; }
    public double Nc { get; }
    public IReadOnlyList<double> RgbD => _rgbD;
    public double Fl { get; }
    public double FlRoot { get; }
    public double Z { get; }

    internal double RgbD0 => _rgbD[0];
    internal double RgbD1 => _rgbD[1];
    internal double RgbD2 => _rgbD[2];

    /// <summary>
    /// sRGB-like viewing conditions: D65 white point, 200 lux, a mid-grey (L* 50) background, average
    /// surround, and an illuminant the eye does not discount.
    /// </summary>
    public static ViewingConditions Make() => Make(
        ColorUtils.WhitePointD65(),
        200.0 / Math.PI * ColorUtils.YFromLstar(50.0) / 100.0,
        50.0,
        2.0,
        false);

    /// <summary>sRGB-like viewing conditions with a custom background L*.</summary>
    public static ViewingConditions DefaultWithBackgroundLstar(double lstar) => Make(
        ColorUtils.WhitePointD65(),
        200.0 / Math.PI * ColorUtils.YFromLstar(50.0) / 100.0,
        lstar,
        2.0,
        false);

    /// <summary>Viewing conditions from a simple, physically relevant set of parameters.</summary>
    /// <param name="whitePoint">White point in XYZ. Default D65, a sunny day afternoon.</param>
    /// <param name="adaptingLuminance">
    /// Luminance of the adapting field: how bright the room is. Lux × 0.0586. Default 11.72 (200 lux).
    /// </param>
    /// <param name="backgroundLstar">L* of the area surrounding the color. Default 50.</param>
    /// <param name="surround">
    /// 0 is pitch dark (a cinema), 1 a dim room (TV at night), 2 no difference between the light on
    /// the color and around it. Default 2.
    /// </param>
    /// <param name="discountingIlluminant">
    /// Whether the eye accounts for the tint of ambient light. Default false: it does not for
    /// self-luminous displays.
    /// </param>
    public static ViewingConditions Make(
        double[] whitePoint, double adaptingLuminance, double backgroundLstar, double surround, bool discountingIlluminant)
    {
        ArgumentNullException.ThrowIfNull(whitePoint);
        // A background of pure black is non-physical and leads to infinities that represent the idea
        // that any color viewed in pure black can't be seen. (Upstream's Java port clamps; its
        // TypeScript port does not. Nothing in this library passes a background below 0.1.)
        backgroundLstar = Math.Max(0.1, backgroundLstar);
        var xyz = whitePoint;
        var rW = xyz[0] * 0.401288 + xyz[1] * 0.650173 + xyz[2] * -0.051461;
        var gW = xyz[0] * -0.250268 + xyz[1] * 1.204414 + xyz[2] * 0.045854;
        var bW = xyz[0] * -0.002079 + xyz[1] * 0.048952 + xyz[2] * 0.953127;
        var f = 0.8 + surround / 10.0;
        var c = f >= 0.9
            ? MathUtils.Lerp(0.59, 0.69, (f - 0.9) * 10.0)
            : MathUtils.Lerp(0.525, 0.59, (f - 0.8) * 10.0);
        var d = discountingIlluminant
            ? 1.0
            : f * (1.0 - 1.0 / 3.6 * Math.Exp((-adaptingLuminance - 42.0) / 92.0));
        d = d > 1.0 ? 1.0 : d < 0.0 ? 0.0 : d;
        var nc = f;
        double[] rgbD =
        [
            d * (100.0 / rW) + 1.0 - d,
            d * (100.0 / gW) + 1.0 - d,
            d * (100.0 / bW) + 1.0 - d,
        ];
        var k = 1.0 / (5.0 * adaptingLuminance + 1.0);
        var k4 = k * k * k * k;
        var k4F = 1.0 - k4;
        var fl = k4 * adaptingLuminance + 0.1 * k4F * k4F * Math.Cbrt(5.0 * adaptingLuminance);
        var n = ColorUtils.YFromLstar(backgroundLstar) / whitePoint[1];
        var z = 1.48 + Math.Sqrt(n);
        var nbb = 0.725 / Math.Pow(n, 0.2);
        var ncb = nbb;
        double[] rgbAFactors =
        [
            Math.Pow(fl * rgbD[0] * rW / 100.0, 0.42),
            Math.Pow(fl * rgbD[1] * gW / 100.0, 0.42),
            Math.Pow(fl * rgbD[2] * bW / 100.0, 0.42),
        ];
        double[] rgbA =
        [
            400.0 * rgbAFactors[0] / (rgbAFactors[0] + 27.13),
            400.0 * rgbAFactors[1] / (rgbAFactors[1] + 27.13),
            400.0 * rgbAFactors[2] / (rgbAFactors[2] + 27.13),
        ];
        var aw = (2.0 * rgbA[0] + rgbA[1] + 0.05 * rgbA[2]) * nbb;
        return new ViewingConditions(n, aw, nbb, ncb, c, nc, rgbD, fl, Math.Pow(fl, 0.25), z);
    }
}
