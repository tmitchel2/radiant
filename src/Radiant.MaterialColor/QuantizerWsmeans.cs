// Copyright 2021 Google LLC
// Licensed under the Apache License, Version 2.0 (see LICENSE in this directory).
// Ported to C# from material-color-utilities (see README.md).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Radiant.MaterialColor;

/// <summary>
/// An image quantizer that improves on the speed of a standard K-Means algorithm by implementing
/// several optimizations, including deduping identical pixels and a triangle inequality rule that
/// reduces the number of comparisons needed to identify which cluster a point should be moved to.
/// <para>
/// Wsmeans stands for Weighted Square Means.
/// </para>
/// <para>
/// This algorithm was designed by M. Emre Celebi, and was found in their 2011 paper, Improving the
/// Performance of K-Means for Color Quantization. https://arxiv.org/abs/1101.0395
/// </para>
/// <para>
/// Follows upstream's Java port, which differs from the TypeScript port in ways that change the
/// results: it assigns points to their initial clusters with <c>java.util.Random</c> seeded with
/// 0x42688 (reproduced by <see cref="JavaRandom"/>), where TypeScript uses unseeded
/// <c>Math.random()</c>; and it sorts each row of the cluster distance table by distance, where
/// TypeScript's <c>sort()</c>, given no comparator, compares the rows' objects as strings, which
/// are all equal, and so leaves them in place.
/// </para>
/// </summary>
public static class QuantizerWsmeans
{
    private const int MaxIterations = 10;
    private const double MinMovementDistance = 3.0;

    /// <summary>
    /// Reduce the number of colors needed to represented the input, minimizing the difference between
    /// the original image and the recolored image.
    /// </summary>
    /// <param name="inputPixels">Colors in ARGB format.</param>
    /// <param name="startingClusters">
    /// Defines the initial state of the quantizer. Passing an array that is the result of Wu
    /// quantization leads to higher quality results. Upstream documents an empty array as fine, but
    /// its Java port then fails (the TypeScript port picks random clusters); this port throws
    /// <see cref="ArgumentException"/> instead. Only as many are used as there are clusters, at most
    /// <paramref name="maxColors"/> and the number of distinct pixels (the Java port fails if there
    /// are more; the TypeScript port ignores the rest, as this one does).
    /// </param>
    /// <param name="maxColors">
    /// The number of colors to divide the image into. A lower number of colors may be returned.
    /// </param>
    /// <returns>
    /// Map with keys of colors in ARGB format, values of how many of the input pixels belong to the
    /// color.
    /// </returns>
    public static Dictionary<int, int> Quantize(int[] inputPixels, int[] startingClusters, int maxColors)
    {
        ArgumentNullException.ThrowIfNull(inputPixels);
        ArgumentNullException.ThrowIfNull(startingClusters);

        // Uses a seeded random number generator to ensure consistent results.
        var random = new JavaRandom(0x42688);

        var pixelToCount = new Dictionary<int, int>();
        var points = new double[inputPixels.Length][];
        var pixels = new int[inputPixels.Length];
        var pointProvider = new PointProviderLab();

        var pointCount = 0;
        for (var i = 0; i < inputPixels.Length; i++)
        {
            var inputPixel = inputPixels[i];
            if (!pixelToCount.TryGetValue(inputPixel, out var pixelCount))
            {
                points[pointCount] = pointProvider.FromInt(inputPixel);
                pixels[pointCount] = inputPixel;
                pointCount++;

                pixelToCount[inputPixel] = 1;
            }
            else
            {
                pixelToCount[inputPixel] = pixelCount + 1;
            }
        }

        var counts = new int[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var pixel = pixels[i];
            var count = pixelToCount[pixel];
            counts[i] = count;
        }

        var clusterCount = Math.Min(maxColors, pointCount);
        if (startingClusters.Length != 0)
        {
            clusterCount = Math.Min(clusterCount, startingClusters.Length);
        }
        else if (clusterCount > 0)
        {
            throw new ArgumentException(
                "Starting clusters are required: upstream's Java port, which this follows, creates none of its own.",
                nameof(startingClusters));
        }

        var clusters = new double[clusterCount][];
        for (var i = 0; i < clusterCount; i++)
        {
            clusters[i] = pointProvider.FromInt(startingClusters[i]);
        }

        var clusterIndices = new int[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            clusterIndices[i] = random.NextInt(clusterCount);
        }

        var indexMatrix = new int[clusterCount][];
        for (var i = 0; i < clusterCount; i++)
        {
            indexMatrix[i] = new int[clusterCount];
        }

        var distanceToIndexMatrix = new DistanceAndIndex[clusterCount][];
        for (var i = 0; i < clusterCount; i++)
        {
            distanceToIndexMatrix[i] = new DistanceAndIndex[clusterCount];
            for (var j = 0; j < clusterCount; j++)
            {
                distanceToIndexMatrix[i][j] = new DistanceAndIndex();
            }
        }

        var pixelCountSums = new int[clusterCount];
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            for (var i = 0; i < clusterCount; i++)
            {
                for (var j = i + 1; j < clusterCount; j++)
                {
                    var distance = pointProvider.Distance(clusters[i], clusters[j]);
                    distanceToIndexMatrix[j][i].Distance = distance;
                    distanceToIndexMatrix[j][i].Index = i;
                    distanceToIndexMatrix[i][j].Distance = distance;
                    distanceToIndexMatrix[i][j].Index = j;
                }
                SortByDistance(distanceToIndexMatrix[i]);
                for (var j = 0; j < clusterCount; j++)
                {
                    indexMatrix[i][j] = distanceToIndexMatrix[i][j].Index;
                }
            }

            var pointsMoved = 0;
            for (var i = 0; i < pointCount; i++)
            {
                var point = points[i];
                var previousClusterIndex = clusterIndices[i];
                var previousCluster = clusters[previousClusterIndex];
                var previousDistance = pointProvider.Distance(point, previousCluster);

                var minimumDistance = previousDistance;
                var newClusterIndex = -1;
                for (var j = 0; j < clusterCount; j++)
                {
                    if (distanceToIndexMatrix[previousClusterIndex][j].Distance >= 4 * previousDistance)
                    {
                        continue;
                    }
                    var distance = pointProvider.Distance(point, clusters[j]);
                    if (distance < minimumDistance)
                    {
                        minimumDistance = distance;
                        newClusterIndex = j;
                    }
                }
                if (newClusterIndex != -1)
                {
                    var distanceChange = Math.Abs(Math.Sqrt(minimumDistance) - Math.Sqrt(previousDistance));
                    if (distanceChange > MinMovementDistance)
                    {
                        pointsMoved++;
                        clusterIndices[i] = newClusterIndex;
                    }
                }
            }

            if (pointsMoved == 0 && iteration != 0)
            {
                break;
            }

            var componentASums = new double[clusterCount];
            var componentBSums = new double[clusterCount];
            var componentCSums = new double[clusterCount];
            Array.Fill(pixelCountSums, 0);
            for (var i = 0; i < pointCount; i++)
            {
                var clusterIndex = clusterIndices[i];
                var point = points[i];
                var count = counts[i];
                pixelCountSums[clusterIndex] += count;
                componentASums[clusterIndex] += point[0] * count;
                componentBSums[clusterIndex] += point[1] * count;
                componentCSums[clusterIndex] += point[2] * count;
            }

            for (var i = 0; i < clusterCount; i++)
            {
                var count = pixelCountSums[i];
                if (count == 0)
                {
                    clusters[i] = [0.0, 0.0, 0.0];
                    continue;
                }
                var a = componentASums[i] / count;
                var b = componentBSums[i] / count;
                var c = componentCSums[i] / count;
                clusters[i][0] = a;
                clusters[i][1] = b;
                clusters[i][2] = c;
            }
        }

        var argbToPopulation = new Dictionary<int, int>();
        for (var i = 0; i < clusterCount; i++)
        {
            var count = pixelCountSums[i];
            if (count == 0)
            {
                continue;
            }

            var possibleNewCluster = pointProvider.ToInt(clusters[i]);
            if (argbToPopulation.ContainsKey(possibleNewCluster))
            {
                continue;
            }

            argbToPopulation[possibleNewCluster] = count;
        }

        return argbToPopulation;
    }

    /// <summary>
    /// Sorts a row of the distance table in place, nearest first, keeping entries at equal distances
    /// in their order: Java's <c>Arrays.sort</c> of objects is a stable merge sort, and
    /// <see cref="Array.Sort{T}(T[])"/> is not stable. The row keeps its objects, so later iterations
    /// write through whichever entry a sort left at each position, as upstream's do. (Distances are
    /// sums of squares, or -1 for the row's unset entry, so .NET's ordering of doubles agrees with
    /// Java's <c>Double.compareTo</c>, which differs only for NaN and -0.0.)
    /// </summary>
    private static void SortByDistance(DistanceAndIndex[] row)
    {
        var sorted = row.OrderBy(entry => entry.Distance).ToArray();
        Array.Copy(sorted, row, row.Length);
    }

    /// <summary>A wrapper for maintaining a table of distances between K-Means clusters.</summary>
    private sealed class DistanceAndIndex
    {
        public int Index { get; set; } = -1;

        public double Distance { get; set; } = -1;
    }
}
