using System;

namespace Radiant.Graphics2D;

/// <summary>
/// The sRGB transfer function (IEC 61966-2-1): the curve between the gamma-encoded values that hex
/// codes, design tools and image files use and the linear-light values the GPU blends in.
/// <para>
/// <b>Radiant draws in linear light.</b> Vertex colours are linear and the surface is an
/// <c>*Srgb</c> format, so the hardware encodes on write and blends in between. That is what makes a
/// 50% white-over-black blend come out perceptually mid-grey (sRGB 188) rather than the too-dark 128
/// a blend on encoded values gives. Anything that arrives encoded — a <c>#rrggbb</c>, an ARGB int from
/// a colour library, a PNG pixel — goes through <see cref="ToLinear(float)"/> once, at the edge.
/// </para>
/// </summary>
public static class SrgbTransfer
{
    // Every 8-bit encoded value decoded once. Decoding bytes is the hot direction (hex parsing, ARGB
    // ints from the colour library, texture data), and a table makes it both exact and free.
    private static readonly float[] s_byteToLinear = BuildByteTable();

    /// <summary>Decodes one gamma-encoded sRGB component in [0, 1] to linear light.</summary>
    public static float ToLinear(float encoded) =>
        encoded <= 0.04045f ? encoded / 12.92f : MathF.Pow((encoded + 0.055f) / 1.055f, 2.4f);

    /// <summary>Decodes one 8-bit sRGB component to linear light.</summary>
    public static float ToLinear(byte encoded) => s_byteToLinear[encoded];

    /// <summary>Encodes one linear-light component in [0, 1] to gamma-encoded sRGB.</summary>
    public static float ToSrgb(float linear) =>
        linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;

    /// <summary>
    /// Encodes one linear-light component to an 8-bit sRGB value, clamping out-of-range input.
    /// Round-trips exactly with <see cref="ToLinear(byte)"/> for every byte.
    /// </summary>
    public static byte ToSrgbByte(float linear)
    {
        var encoded = ToSrgb(Math.Clamp(linear, 0f, 1f));
        return (byte)MathF.Round(encoded * 255f);
    }

    private static float[] BuildByteTable()
    {
        var table = new float[256];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = ToLinear(i / 255f);
        }
        return table;
    }
}
