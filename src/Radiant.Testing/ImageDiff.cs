using System;

namespace Radiant.Testing;

/// <summary>
/// Compares images as a person would see them: each pair of pixels, blended over white, differs
/// by their distance in YIQ (brightness weighted over hue), the measure pixelmatch uses.
/// </summary>
public static class ImageDiff
{
    // The largest YIQ distance, between black and white.
    private const double MaxDelta = 35215.0;

    /// <summary>Compares two images of the same size.</summary>
    /// <param name="actual">One image.</param>
    /// <param name="expected">The other.</param>
    /// <param name="threshold">The largest difference that counts as the same, from 0 to 1.</param>
    public static ImageDiffResult Compare(Snapshot actual, Snapshot expected, double threshold = 0.1)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            throw new ArgumentException($"The images differ in size: {actual.Width}×{actual.Height} and {expected.Width}×{expected.Height}.");
        }
        var limit = MaxDelta * threshold * threshold;
        var diff = new byte[actual.Rgba.Length];
        var different = 0;
        for (var i = 0; i < diff.Length; i += 4)
        {
            var delta = Delta(actual.Rgba, expected.Rgba, i);
            if (delta > limit)
            {
                different++;
                (diff[i], diff[i + 1], diff[i + 2], diff[i + 3]) = (255, 0, 0, 255);
            }
            else
            {
                // The image, faded to a light grey so the red stands out.
                var grey = (byte)(255 - (255 - Luma(actual.Rgba, i)) * 0.25);
                (diff[i], diff[i + 1], diff[i + 2], diff[i + 3]) = (grey, grey, grey, 255);
            }
        }
        return new ImageDiffResult(different, new Snapshot(actual.Width, actual.Height, diff));
    }

    private static double Delta(byte[] a, byte[] b, int i)
    {
        var (r1, g1, b1) = OverWhite(a, i);
        var (r2, g2, b2) = OverWhite(b, i);
        var y = Y(r1, g1, b1) - Y(r2, g2, b2);
        var iq = I(r1, g1, b1) - I(r2, g2, b2);
        var q = Q(r1, g1, b1) - Q(r2, g2, b2);
        return 0.5053 * y * y + 0.299 * iq * iq + 0.1957 * q * q;
    }

    private static (double R, double G, double B) OverWhite(byte[] p, int i)
    {
        var alpha = p[i + 3] / 255.0;
        return (255 + (p[i] - 255) * alpha, 255 + (p[i + 1] - 255) * alpha, 255 + (p[i + 2] - 255) * alpha);
    }

    private static double Luma(byte[] p, int i)
    {
        var (r, g, b) = OverWhite(p, i);
        return Y(r, g, b);
    }

    private static double Y(double r, double g, double b) => r * 0.29889531 + g * 0.58662247 + b * 0.11448223;

    private static double I(double r, double g, double b) => r * 0.59597799 - g * 0.27417610 - b * 0.32180189;

    private static double Q(double r, double g, double b) => r * 0.21147017 - g * 0.52261711 + b * 0.31114694;
}
