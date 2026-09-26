// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

namespace Radiant.ColorSystem;

/// <summary>An interface to allow use of different color spaces by quantizers.</summary>
public interface IPointProvider
{
    /// <summary>The coordinates, in this provider's color space, of an sRGB color in ARGB format.</summary>
    double[] FromInt(int argb);

    /// <summary>The ARGB (i.e. hex code) representation of a point in this provider's color space.</summary>
    int ToInt(double[] point);

    /// <summary>
    /// Squared distance between two colors. Distance is defined by scientific color spaces and
    /// referred to as delta E.
    /// </summary>
    double Distance(double[] one, double[] two);
}
