using System;
using Radiant.UI.Core;

namespace Radiant.Gallery;

/// <summary>Pictures made in code for the sample pages: soft two-hue gradients with a round highlight.</summary>
internal static class SamplePictures
{
    public static ImageSource Gradient(float hue, int width = 320, int height = 240)
    {
        var pixels = new byte[width * height * 4];
        var (r1, g1, b1) = Hsl(hue, 0.55f, 0.72f);
        var (r2, g2, b2) = Hsl((hue + 40f) % 360f, 0.6f, 0.45f);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var t = (x / (float)width + y / (float)height) / 2f;
                var dx = x - width * 0.62f;
                var dy = y - height * 0.4f;
                var glow = MathF.Max(0f, 1f - MathF.Sqrt(dx * dx + dy * dy) / (height * 0.45f));
                var i = (y * width + x) * 4;
                pixels[i + 2] = Mix(r1, r2, t, glow);
                pixels[i + 1] = Mix(g1, g2, t, glow);
                pixels[i] = Mix(b1, b2, t, glow);
                pixels[i + 3] = 255;
            }
        }
        return ImageSource.FromBgra(width, height, pixels);

        static byte Mix(float a, float b, float t, float glow) => (byte)Math.Clamp((a + (b - a) * t + (1f - (a + (b - a) * t)) * glow * 0.6f) * 255f, 0f, 255f);
    }

    private static (float R, float G, float B) Hsl(float h, float s, float l)
    {
        var c = (1f - MathF.Abs(2f * l - 1f)) * s;
        var x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        var m = l - c / 2f;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0f),
            < 120 => (x, c, 0f),
            < 180 => (0f, c, x),
            < 240 => (0f, x, c),
            < 300 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return (r + m, g + m, b + m);
    }
}
