// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;

namespace Radiant.MaterialColor;

/// <summary>
/// An image quantizer that divides the image's pixels into clusters by recursively cutting an RGB
/// cube, based on the weight of pixels in each area of the cube.
/// <para>
/// The algorithm was described by Xiaolin Wu in Graphic Gems II, published in 1991.
/// </para>
/// <para>
/// Follows upstream's Java port, whose results differ from the TypeScript port's: its moments are
/// 32-bit integers, whose products wrap on overflow where TypeScript's doubles do not, and a cube's
/// color is its mean channel truncated, where TypeScript rounds it.
/// </para>
/// </summary>
public sealed class QuantizerWu
{
    // A histogram of all the input colors is constructed. It has the shape of a
    // cube. The cube would be too large if it contained all 16 million colors:
    // historical best practice is to use 5 bits  of the 8 in each channel,
    // reducing the histogram to a volume of ~32,000.
    private const int IndexBits = 5;
    private const int IndexCount = 33; // ((1 << IndexBits) + 1)
    private const int TotalSize = 35937; // IndexCount * IndexCount * IndexCount

    private int[] _weights = [];
    private int[] _momentsR = [];
    private int[] _momentsG = [];
    private int[] _momentsB = [];
    private double[] _moments = [];
    private Box[] _cubes = [];

    private enum Direction
    {
        Red,
        Green,
        Blue,
    }

    /// <summary>
    /// Divides <paramref name="pixels"/> into at most <paramref name="maxColors"/> clusters.
    /// </summary>
    /// <param name="pixels">Colors in ARGB format.</param>
    /// <param name="maxColors">
    /// The number of colors to divide the image into. A lower number of colors may be returned.
    /// </param>
    /// <returns>
    /// Colors in ARGB format, without duplicates (upstream's Java port collects them into a map).
    /// </returns>
    public int[] Quantize(int[] pixels, int maxColors)
    {
        var mapResult = QuantizerMap.Quantize(pixels);
        ConstructHistogram(mapResult);
        CreateMoments();
        var resultCount = CreateBoxes(maxColors);
        var colors = CreateResult(resultCount);
        var resultMap = new List<int>(colors.Count);
        var seen = new HashSet<int>();
        foreach (var color in colors)
        {
            if (seen.Add(color))
            {
                resultMap.Add(color);
            }
        }
        return [.. resultMap];
    }

    private static int GetIndex(int r, int g, int b) =>
        (r << (IndexBits * 2)) + (r << (IndexBits + 1)) + r + (g << IndexBits) + g + b;

    // Java's int arithmetic wraps on overflow, and the results depend on it, so every sum and
    // product of moments here is unchecked 32-bit arithmetic, as upstream's Java port has it.
    private void ConstructHistogram(Dictionary<int, int> pixels)
    {
        _weights = new int[TotalSize];
        _momentsR = new int[TotalSize];
        _momentsG = new int[TotalSize];
        _momentsB = new int[TotalSize];
        _moments = new double[TotalSize];

        foreach (var (pixel, count) in pixels)
        {
            var red = ColorUtils.RedFromArgb(pixel);
            var green = ColorUtils.GreenFromArgb(pixel);
            var blue = ColorUtils.BlueFromArgb(pixel);
            const int bitsToRemove = 8 - IndexBits;
            var iR = (red >> bitsToRemove) + 1;
            var iG = (green >> bitsToRemove) + 1;
            var iB = (blue >> bitsToRemove) + 1;
            var index = GetIndex(iR, iG, iB);
            unchecked
            {
                _weights[index] += count;
                _momentsR[index] += red * count;
                _momentsG[index] += green * count;
                _momentsB[index] += blue * count;
                _moments[index] += count * ((red * red) + (green * green) + (blue * blue));
            }
        }
    }

    private void CreateMoments()
    {
        for (var r = 1; r < IndexCount; ++r)
        {
            var area = new int[IndexCount];
            var areaR = new int[IndexCount];
            var areaG = new int[IndexCount];
            var areaB = new int[IndexCount];
            var area2 = new double[IndexCount];

            for (var g = 1; g < IndexCount; ++g)
            {
                var line = 0;
                var lineR = 0;
                var lineG = 0;
                var lineB = 0;
                var line2 = 0.0;
                for (var b = 1; b < IndexCount; ++b)
                {
                    var index = GetIndex(r, g, b);
                    unchecked
                    {
                        line += _weights[index];
                        lineR += _momentsR[index];
                        lineG += _momentsG[index];
                        lineB += _momentsB[index];
                        line2 += _moments[index];

                        area[b] += line;
                        areaR[b] += lineR;
                        areaG[b] += lineG;
                        areaB[b] += lineB;
                        area2[b] += line2;

                        var previousIndex = GetIndex(r - 1, g, b);
                        _weights[index] = _weights[previousIndex] + area[b];
                        _momentsR[index] = _momentsR[previousIndex] + areaR[b];
                        _momentsG[index] = _momentsG[previousIndex] + areaG[b];
                        _momentsB[index] = _momentsB[previousIndex] + areaB[b];
                        _moments[index] = _moments[previousIndex] + area2[b];
                    }
                }
            }
        }
    }

    private int CreateBoxes(int maxColorCount)
    {
        _cubes = new Box[maxColorCount];
        for (var i = 0; i < maxColorCount; i++)
        {
            _cubes[i] = new Box();
        }
        var volumeVariance = new double[maxColorCount];
        var firstBox = _cubes[0];
        firstBox.R1 = IndexCount - 1;
        firstBox.G1 = IndexCount - 1;
        firstBox.B1 = IndexCount - 1;

        var generatedColorCount = maxColorCount;
        var next = 0;
        for (var i = 1; i < maxColorCount; i++)
        {
            if (Cut(_cubes[next], _cubes[i]))
            {
                volumeVariance[next] = (_cubes[next].Vol > 1) ? Variance(_cubes[next]) : 0.0;
                volumeVariance[i] = (_cubes[i].Vol > 1) ? Variance(_cubes[i]) : 0.0;
            }
            else
            {
                volumeVariance[next] = 0.0;
                i--;
            }

            next = 0;

            var temp = volumeVariance[0];
            for (var j = 1; j <= i; j++)
            {
                if (volumeVariance[j] > temp)
                {
                    temp = volumeVariance[j];
                    next = j;
                }
            }
            if (temp <= 0.0)
            {
                generatedColorCount = i + 1;
                break;
            }
        }

        return generatedColorCount;
    }

    private List<int> CreateResult(int colorCount)
    {
        var colors = new List<int>();
        for (var i = 0; i < colorCount; ++i)
        {
            var cube = _cubes[i];
            var weight = Volume(cube, _weights);
            if (weight > 0)
            {
                // Integer division, as in upstream's Java port; the TypeScript port rounds.
                var r = Volume(cube, _momentsR) / weight;
                var g = Volume(cube, _momentsG) / weight;
                var b = Volume(cube, _momentsB) / weight;
                var color = (255 << 24) | ((r & 0x0ff) << 16) | ((g & 0x0ff) << 8) | (b & 0x0ff);
                colors.Add(color);
            }
        }
        return colors;
    }

    private double Variance(Box cube)
    {
        var dr = Volume(cube, _momentsR);
        var dg = Volume(cube, _momentsG);
        var db = Volume(cube, _momentsB);
        var xx =
            _moments[GetIndex(cube.R1, cube.G1, cube.B1)]
            - _moments[GetIndex(cube.R1, cube.G1, cube.B0)]
            - _moments[GetIndex(cube.R1, cube.G0, cube.B1)]
            + _moments[GetIndex(cube.R1, cube.G0, cube.B0)]
            - _moments[GetIndex(cube.R0, cube.G1, cube.B1)]
            + _moments[GetIndex(cube.R0, cube.G1, cube.B0)]
            + _moments[GetIndex(cube.R0, cube.G0, cube.B1)]
            - _moments[GetIndex(cube.R0, cube.G0, cube.B0)];

        var hypotenuse = unchecked(dr * dr + dg * dg + db * db);
        var volume = Volume(cube, _weights);
        return xx - hypotenuse / ((double)volume);
    }

    private bool Cut(Box one, Box two)
    {
        var wholeR = Volume(one, _momentsR);
        var wholeG = Volume(one, _momentsG);
        var wholeB = Volume(one, _momentsB);
        var wholeW = Volume(one, _weights);

        var maxRResult = Maximize(one, Direction.Red, one.R0 + 1, one.R1, wholeR, wholeG, wholeB, wholeW);
        var maxGResult = Maximize(one, Direction.Green, one.G0 + 1, one.G1, wholeR, wholeG, wholeB, wholeW);
        var maxBResult = Maximize(one, Direction.Blue, one.B0 + 1, one.B1, wholeR, wholeG, wholeB, wholeW);
        Direction cutDirection;
        var maxR = maxRResult.Maximum;
        var maxG = maxGResult.Maximum;
        var maxB = maxBResult.Maximum;
        if (maxR >= maxG && maxR >= maxB)
        {
            if (maxRResult.CutLocation < 0)
            {
                return false;
            }
            cutDirection = Direction.Red;
        }
        else if (maxG >= maxR && maxG >= maxB)
        {
            cutDirection = Direction.Green;
        }
        else
        {
            cutDirection = Direction.Blue;
        }

        two.R1 = one.R1;
        two.G1 = one.G1;
        two.B1 = one.B1;

        switch (cutDirection)
        {
            case Direction.Red:
                one.R1 = maxRResult.CutLocation;
                two.R0 = one.R1;
                two.G0 = one.G0;
                two.B0 = one.B0;
                break;
            case Direction.Green:
                one.G1 = maxGResult.CutLocation;
                two.R0 = one.R0;
                two.G0 = one.G1;
                two.B0 = one.B0;
                break;
            case Direction.Blue:
                one.B1 = maxBResult.CutLocation;
                two.R0 = one.R0;
                two.G0 = one.G0;
                two.B0 = one.B1;
                break;
        }

        one.Vol = (one.R1 - one.R0) * (one.G1 - one.G0) * (one.B1 - one.B0);
        two.Vol = (two.R1 - two.R0) * (two.G1 - two.G0) * (two.B1 - two.B0);

        return true;
    }

    private MaximizeResult Maximize(
        Box cube,
        Direction direction,
        int first,
        int last,
        int wholeR,
        int wholeG,
        int wholeB,
        int wholeW)
    {
        var bottomR = Bottom(cube, direction, _momentsR);
        var bottomG = Bottom(cube, direction, _momentsG);
        var bottomB = Bottom(cube, direction, _momentsB);
        var bottomW = Bottom(cube, direction, _weights);

        var max = 0.0;
        var cut = -1;

        for (var i = first; i < last; i++)
        {
            var halfR = unchecked(bottomR + Top(cube, direction, i, _momentsR));
            var halfG = unchecked(bottomG + Top(cube, direction, i, _momentsG));
            var halfB = unchecked(bottomB + Top(cube, direction, i, _momentsB));
            var halfW = unchecked(bottomW + Top(cube, direction, i, _weights));
            if (halfW == 0)
            {
                continue;
            }

            // The numerator is summed in (wrapping) int arithmetic before it becomes a double.
            double tempNumerator = unchecked(halfR * halfR + halfG * halfG + halfB * halfB);
            double tempDenominator = halfW;
            var temp = tempNumerator / tempDenominator;

            unchecked
            {
                halfR = wholeR - halfR;
                halfG = wholeG - halfG;
                halfB = wholeB - halfB;
                halfW = wholeW - halfW;
            }
            if (halfW == 0)
            {
                continue;
            }

            tempNumerator = unchecked(halfR * halfR + halfG * halfG + halfB * halfB);
            tempDenominator = halfW;
            temp += tempNumerator / tempDenominator;

            if (temp > max)
            {
                max = temp;
                cut = i;
            }
        }
        return new MaximizeResult(cut, max);
    }

    private static int Volume(Box cube, int[] moment) =>
        unchecked(
            moment[GetIndex(cube.R1, cube.G1, cube.B1)]
            - moment[GetIndex(cube.R1, cube.G1, cube.B0)]
            - moment[GetIndex(cube.R1, cube.G0, cube.B1)]
            + moment[GetIndex(cube.R1, cube.G0, cube.B0)]
            - moment[GetIndex(cube.R0, cube.G1, cube.B1)]
            + moment[GetIndex(cube.R0, cube.G1, cube.B0)]
            + moment[GetIndex(cube.R0, cube.G0, cube.B1)]
            - moment[GetIndex(cube.R0, cube.G0, cube.B0)]);

    private static int Bottom(Box cube, Direction direction, int[] moment) =>
        unchecked(direction switch
        {
            Direction.Red =>
                -moment[GetIndex(cube.R0, cube.G1, cube.B1)]
                + moment[GetIndex(cube.R0, cube.G1, cube.B0)]
                + moment[GetIndex(cube.R0, cube.G0, cube.B1)]
                - moment[GetIndex(cube.R0, cube.G0, cube.B0)],
            Direction.Green =>
                -moment[GetIndex(cube.R1, cube.G0, cube.B1)]
                + moment[GetIndex(cube.R1, cube.G0, cube.B0)]
                + moment[GetIndex(cube.R0, cube.G0, cube.B1)]
                - moment[GetIndex(cube.R0, cube.G0, cube.B0)],
            Direction.Blue =>
                -moment[GetIndex(cube.R1, cube.G1, cube.B0)]
                + moment[GetIndex(cube.R1, cube.G0, cube.B0)]
                + moment[GetIndex(cube.R0, cube.G1, cube.B0)]
                - moment[GetIndex(cube.R0, cube.G0, cube.B0)],
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        });

    private static int Top(Box cube, Direction direction, int position, int[] moment) =>
        unchecked(direction switch
        {
            Direction.Red =>
                moment[GetIndex(position, cube.G1, cube.B1)]
                - moment[GetIndex(position, cube.G1, cube.B0)]
                - moment[GetIndex(position, cube.G0, cube.B1)]
                + moment[GetIndex(position, cube.G0, cube.B0)],
            Direction.Green =>
                moment[GetIndex(cube.R1, position, cube.B1)]
                - moment[GetIndex(cube.R1, position, cube.B0)]
                - moment[GetIndex(cube.R0, position, cube.B1)]
                + moment[GetIndex(cube.R0, position, cube.B0)],
            Direction.Blue =>
                moment[GetIndex(cube.R1, cube.G1, position)]
                - moment[GetIndex(cube.R1, cube.G0, position)]
                - moment[GetIndex(cube.R0, cube.G1, position)]
                + moment[GetIndex(cube.R0, cube.G0, position)],
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        });

    /// <summary>
    /// The result of calculating where to cut an existing box in such a way to maximize variance
    /// between the two new boxes created by a cut.
    /// </summary>
    /// <param name="CutLocation">Where to cut; negative if a cut is impossible.</param>
    /// <param name="Maximum">The variance the cut achieves.</param>
    private readonly record struct MaximizeResult(int CutLocation, double Maximum);

    /// <summary>
    /// Keeps track of the state of each box created as the Wu quantization algorithm progresses
    /// through dividing the image's pixels as plotted in RGB.
    /// </summary>
    private sealed class Box
    {
        public int R0 { get; set; }

        public int R1 { get; set; }

        public int G0 { get; set; }

        public int G1 { get; set; }

        public int B0 { get; set; }

        public int B1 { get; set; }

        public int Vol { get; set; }
    }
}
