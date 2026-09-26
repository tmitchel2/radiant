// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;

namespace Radiant.MaterialColor;

/// <summary>
/// Provides conversions needed for K-Means quantization. Converting input to points, and converting
/// the final state of the K-Means algorithm to colors.
/// </summary>
public sealed class PointProviderLab : IPointProvider
{
    /// <summary>
    /// Convert a color represented in ARGB to a 3-element array of L*a*b* coordinates of the color.
    /// </summary>
    public double[] FromInt(int argb) => ColorUtils.LabFromArgb(argb);

    /// <summary>Convert a 3-element array of L*a*b* coordinates to a color represented in ARGB.</summary>
    public int ToInt(double[] point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return ColorUtils.ArgbFromLab(point[0], point[1], point[2]);
    }

    /// <summary>
    /// Standard CIE 1976 delta E formula also takes the square root, unneeded here. This method is
    /// used by quantization algorithms to compare distance, and the relative ordering is the same,
    /// with or without a square root.
    /// <para>
    /// This relatively minor optimization is helpful because this method is called at least once for
    /// each pixel in an image.
    /// </para>
    /// </summary>
    public double Distance(double[] one, double[] two)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(two);
        var dL = one[0] - two[0];
        var dA = one[1] - two[1];
        var dB = one[2] - two[2];
        return dL * dL + dA * dA + dB * dB;
    }
}
