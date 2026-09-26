// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>
/// Quantizes an image into a map, with keys of ARGB colors, and values of the number of times that
/// color appears in the image.
/// </summary>
public static class QuantizerMap
{
    /// <summary>
    /// Counts each color in <paramref name="pixels"/>.
    /// <para>
    /// Follows upstream's Java port, which counts every pixel; the TypeScript port skips pixels that
    /// are not fully opaque.
    /// </para>
    /// </summary>
    /// <param name="pixels">Colors in ARGB format.</param>
    /// <returns>
    /// A map with keys of ARGB colors, in the order they first appear, and values of the number of
    /// times the color appears in the image.
    /// </returns>
    public static Dictionary<int, int> Quantize(int[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var countByColor = new Dictionary<int, int>();
        foreach (var pixel in pixels)
        {
            countByColor.TryGetValue(pixel, out var currentPixelCount);
            countByColor[pixel] = currentPixelCount + 1;
        }
        return countByColor;
    }
}
