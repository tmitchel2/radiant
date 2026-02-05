using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D.Visual
{
    internal sealed class ComparisonResult
    {
        public int MismatchCount { get; init; }
        public int TotalPixels { get; init; }
        public double MismatchPercentage => TotalPixels > 0 ? (double)MismatchCount / TotalPixels * 100 : 0;
        public Image<Rgba32>? DiffImage { get; init; }
        public bool Passed => MismatchCount == 0;
    }

    internal static class GoldenImageHelper
    {
        private static string GetProjectRoot([CallerFilePath] string? callerFilePath = null)
        {
            // This file lives at src/Radiant.Tests/Graphics2D/Visual/GoldenImageHelper.cs
            // Project root is 4 levels up
            var dir = Path.GetDirectoryName(callerFilePath)!;
            return Path.GetFullPath(Path.Combine(dir, "..", ".."));
        }

        private static string GetGoldenDir()
        {
            return Path.Combine(GetProjectRoot(), "TestData", "Golden");
        }

        private static string GetResultsDir()
        {
            return Path.Combine(GetProjectRoot(), "TestResults");
        }

        public static void AssertMatchesGolden(Image<Rgba32> actual, string testName, int tolerance = 2)
        {
            var goldenDir = GetGoldenDir();
            var goldenPath = Path.Combine(goldenDir, $"{testName}.png");

            var forceUpdate = Environment.GetEnvironmentVariable("UPDATE_GOLDEN_IMAGES") == "true";

            if (forceUpdate || !File.Exists(goldenPath))
            {
                Directory.CreateDirectory(goldenDir);
                actual.SaveAsPng(goldenPath);

                if (forceUpdate)
                    return;

                Assert.Inconclusive(
                    $"Golden image generated: {goldenPath}. Review and commit.");
            }

            using var expected = Image.Load<Rgba32>(goldenPath);
            var result = Compare(actual, expected, tolerance);

            if (!result.Passed)
            {
                // Save failure artifacts
                var resultsDir = GetResultsDir();
                Directory.CreateDirectory(resultsDir);

                actual.SaveAsPng(Path.Combine(resultsDir, $"{testName}_actual.png"));
                result.DiffImage?.SaveAsPng(Path.Combine(resultsDir, $"{testName}_diff.png"));

                Assert.Fail(
                    $"Golden image mismatch: {result.MismatchCount}/{result.TotalPixels} pixels " +
                    $"({result.MismatchPercentage:F2}%) exceeded tolerance of {tolerance}. " +
                    $"See TestResults/{testName}_actual.png and TestResults/{testName}_diff.png");
            }
        }

        public static ComparisonResult Compare(Image<Rgba32> actual, Image<Rgba32> expected, int tolerance = 2)
        {
            if (actual.Width != expected.Width || actual.Height != expected.Height)
            {
                return new ComparisonResult
                {
                    MismatchCount = actual.Width * actual.Height,
                    TotalPixels = actual.Width * actual.Height
                };
            }

            var width = actual.Width;
            var height = actual.Height;
            var diff = new Image<Rgba32>(width, height);
            var mismatchCount = 0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var a = actual[x, y];
                    var e = expected[x, y];

                    if (Math.Abs(a.R - e.R) > tolerance ||
                        Math.Abs(a.G - e.G) > tolerance ||
                        Math.Abs(a.B - e.B) > tolerance ||
                        Math.Abs(a.A - e.A) > tolerance)
                    {
                        mismatchCount++;
                        diff[x, y] = new Rgba32(255, 0, 0, 255); // Red for mismatch
                    }
                    else
                    {
                        diff[x, y] = new Rgba32(0, 0, 0, 255); // Black for match
                    }
                }
            }

            return new ComparisonResult
            {
                MismatchCount = mismatchCount,
                TotalPixels = width * height,
                DiffImage = diff
            };
        }

        public static void AssertImagesIdentical(Image<Rgba32> imageA, Image<Rgba32> imageB, int tolerance = 2)
        {
            var result = Compare(imageA, imageB, tolerance);
            if (!result.Passed)
            {
                result.DiffImage?.Dispose();
                Assert.Fail(
                    $"Images differ: {result.MismatchCount}/{result.TotalPixels} pixels " +
                    $"({result.MismatchPercentage:F2}%) exceeded tolerance of {tolerance}.");
            }
            result.DiffImage?.Dispose();
        }
    }
}
