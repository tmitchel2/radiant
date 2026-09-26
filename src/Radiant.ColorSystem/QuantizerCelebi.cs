// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System.Collections.Generic;

namespace Radiant.ColorSystem;

/// <summary>
/// An image quantizer that improves on the quality of a standard K-Means algorithm by setting the
/// K-Means initial state to the output of a Wu quantizer, instead of random centroids. Improves on
/// speed by several optimizations, as implemented in Wsmeans, or Weighted Square Means, K-Means with
/// those optimizations.
/// <para>
/// This algorithm was designed by M. Emre Celebi, and was found in their 2011 paper, Improving the
/// Performance of K-Means for Color Quantization. https://arxiv.org/abs/1101.0395
/// </para>
/// </summary>
public static class QuantizerCelebi
{
    /// <summary>
    /// Reduce the number of colors needed to represented the input, minimizing the difference between
    /// the original image and the recolored image.
    /// </summary>
    /// <param name="pixels">Colors in ARGB format.</param>
    /// <param name="maxColors">
    /// The number of colors to divide the image into. A lower number of colors may be returned.
    /// </param>
    /// <returns>
    /// Map with keys of colors in ARGB format, and values of number of pixels in the original image
    /// that correspond to the color in the quantized image.
    /// </returns>
    public static Dictionary<int, int> Quantize(int[] pixels, int maxColors)
    {
        var wu = new QuantizerWu();
        var wuClusters = wu.Quantize(pixels, maxColors);
        return QuantizerWsmeans.Quantize(pixels, wuClusters, maxColors);
    }
}
