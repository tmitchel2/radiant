// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Globalization;

namespace Radiant.ColorSystem;

/// <summary>
/// HCT: hue, chroma and tone. A color system built on CAM16 hue and chroma and L* (from L*a*b*) for
/// tone, which gives a perceptually accurate color measurement that can also account for the
/// lighting a color is seen in.
/// <para>
/// Using L* ties the color system to contrast, and so to accessibility. Contrast ratio depends on
/// relative luminance (Y in XYZ), and L* is Y made linear to human perception: a tone difference of
/// 40 guarantees a contrast ratio of at least 3.0, and 50 at least 4.5.
/// </para>
/// <para>
/// Immutable, unlike upstream's (whose hue, chroma and tone setters re-solve in place): the
/// <c>With*</c> methods return the re-solved color instead.
/// </para>
/// </summary>
public sealed class Hct
{
    private readonly int _argb;

    private Hct(int argb)
    {
        var cam = Cam16.FromInt(argb);
        Hue = cam.Hue;
        Chroma = cam.Chroma;
        Tone = ColorUtils.LstarFromArgb(argb);
        _argb = argb;
    }

    /// <summary>Hue, 0 (inclusive) to 360 (exclusive).</summary>
    public double Hue { get; }

    /// <summary>
    /// Chroma, informally colorfulness. Its maximum depends on hue and tone, so the color may have
    /// less than was asked for.
    /// </summary>
    public double Chroma { get; }

    /// <summary>Lightness, 0 to 100.</summary>
    public double Tone { get; }

    /// <summary>An HCT color from hue, chroma and tone; out-of-range hue and tone are corrected.</summary>
    public static Hct From(double hue, double chroma, double tone) => new(HctSolver.SolveToInt(hue, chroma, tone));

    /// <summary>The HCT representation of an ARGB color, in default viewing conditions.</summary>
    public static Hct FromInt(int argb) => new(argb);

    /// <summary>The ARGB representation of this color.</summary>
    public int ToInt() => _argb;

    /// <summary>This color with a different hue. Chroma may decrease: its maximum depends on hue and tone.</summary>
    public Hct WithHue(double hue) => new(HctSolver.SolveToInt(hue, Chroma, Tone));

    /// <summary>This color with a different chroma, which may be reduced to fit the gamut.</summary>
    public Hct WithChroma(double chroma) => new(HctSolver.SolveToInt(Hue, chroma, Tone));

    /// <summary>This color with a different tone. Chroma may decrease: its maximum depends on hue and tone.</summary>
    public Hct WithTone(double tone) => new(HctSolver.SolveToInt(Hue, Chroma, tone));

    /// <summary>Whether a hue is in the blue range.</summary>
    public static bool IsBlue(double hue) => hue >= 250 && hue < 270;

    /// <summary>Whether a hue is in the yellow range.</summary>
    public static bool IsYellow(double hue) => hue >= 105 && hue < 125;

    /// <summary>Whether a hue is in the cyan range.</summary>
    public static bool IsCyan(double hue) => hue >= 170 && hue < 207;

    /// <summary>
    /// This color as it appears in different viewing conditions. Colors look different with the
    /// lights on than off, and the same hex on white than on black (color relativity, as Josef Albers
    /// explored in <i>Interaction of Color</i>). See <see cref="ViewingConditions.Make()"/>.
    /// </summary>
    public Hct InViewingConditions(ViewingConditions vc)
    {
        // 1. Use CAM16 to find XYZ coordinates of color in specified VC.
        var cam = Cam16.FromInt(ToInt());
        var viewedInVc = cam.XyzInViewingConditions(vc);

        // 2. Create CAM16 of those XYZ coordinates in default VC.
        var recastInVc = Cam16.FromXyzInViewingConditions(
            viewedInVc[0], viewedInVc[1], viewedInVc[2], ViewingConditions.Make());

        // 3. Create HCT from:
        // - CAM16 using default VC with XYZ coordinates in specified VC.
        // - L* converted from Y in XYZ coordinates in specified VC.
        return From(recastInVc.Hue, recastInVc.Chroma, ColorUtils.LstarFromY(viewedInVc[1]));
    }

    /// <summary>For example <c>HCT(282, 48, 40)</c>.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"HCT({MathUtils.Round(Hue)}, {MathUtils.Round(Chroma)}, {MathUtils.Round(Tone)})");
}
