using System;
using System.Globalization;
using System.Numerics;

namespace Radiant.Graphics2D;

/// <summary>
/// An RGBA colour in <b>linear light</b> with <b>straight (unpremultiplied) alpha</b>, as floats.
/// <para>
/// Linear because that is what the renderer draws and blends in (see <see cref="SrgbTransfer"/>).
/// Straight alpha because it is the form people reason in: <see cref="WithAlpha"/> fades a colour
/// without changing its hue, and the RGB of a fully transparent colour still means something to an
/// animation that fades it in. The renderer premultiplies on the GPU, so nothing a caller passes has
/// to be premultiplied.
/// </para>
/// <para>
/// Encoded colours come in at the edges and are decoded exactly once: <see cref="FromArgb(uint)"/>
/// for the <c>0xAARRGGBB</c> ints a colour library such as Material's produces,
/// <see cref="Parse(ReadOnlySpan{char})"/> for CSS-style hex. The implicit conversion to
/// <see cref="Vector4"/> lets a <see cref="Color"/> go anywhere the renderer takes a colour.
/// </para>
/// </summary>
/// <param name="R">Red, linear light, nominally 0–1.</param>
/// <param name="G">Green, linear light, nominally 0–1.</param>
/// <param name="B">Blue, linear light, nominally 0–1.</param>
/// <param name="A">Coverage, 0 (transparent) to 1 (opaque). Not premultiplied into RGB.</param>
public readonly record struct Color(float R, float G, float B, float A = 1f)
{
    /// <summary>Fully transparent black.</summary>
    public static Color Transparent { get; } = new(0f, 0f, 0f, 0f);

    /// <summary>Opaque black.</summary>
    public static Color Black { get; } = new(0f, 0f, 0f);

    /// <summary>Opaque white.</summary>
    public static Color White { get; } = new(1f, 1f, 1f);

    /// <summary>
    /// A colour from a gamma-encoded <c>0xAARRGGBB</c> value — the packing Android, Material Color
    /// Utilities and most colour pickers use.
    /// </summary>
    public static Color FromArgb(uint argb) => FromSrgb8(
        (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

    /// <summary>
    /// A colour from a gamma-encoded <c>0xAARRGGBB</c> held in a signed int, as Material Color
    /// Utilities returns it (opaque colours are negative).
    /// </summary>
    public static Color FromArgb(int argb) => FromArgb(unchecked((uint)argb));

    /// <summary>A colour from gamma-encoded 8-bit sRGB components.</summary>
    public static Color FromSrgb8(byte r, byte g, byte b, byte a = 255) => new(
        SrgbTransfer.ToLinear(r), SrgbTransfer.ToLinear(g), SrgbTransfer.ToLinear(b), a / 255f);

    /// <summary>A colour from gamma-encoded sRGB components in [0, 1].</summary>
    public static Color FromSrgb(float r, float g, float b, float a = 1f) => new(
        SrgbTransfer.ToLinear(r), SrgbTransfer.ToLinear(g), SrgbTransfer.ToLinear(b), a);

    /// <summary>A colour from a linear-light, straight-alpha <see cref="Vector4"/> (X=R … W=A).</summary>
    public static Color FromVector4(Vector4 linear) => new(linear.X, linear.Y, linear.Z, linear.W);

    /// <summary>
    /// Parses CSS-style hex: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>, with or
    /// without the <c>#</c>. The digits are gamma-encoded sRGB and alpha comes last, as in CSS
    /// (not first, as in <see cref="FromArgb(uint)"/>).
    /// </summary>
    /// <exception cref="FormatException">The text is not one of those forms.</exception>
    public static Color Parse(ReadOnlySpan<char> hex) =>
        TryParse(hex, out var color)
            ? color
            : throw new FormatException($"'{hex.ToString()}' is not a #rgb, #rgba, #rrggbb or #rrggbbaa colour.");

    /// <summary>Parses CSS-style hex; see <see cref="Parse(ReadOnlySpan{char})"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out Color color)
    {
        color = default;
        if (hex.StartsWith("#"))
        {
            hex = hex[1..];
        }

        if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        switch (hex.Length)
        {
            case 3:
                color = FromSrgb8(Expand(value >> 8), Expand(value >> 4), Expand(value));
                return true;
            case 4:
                color = FromSrgb8(Expand(value >> 12), Expand(value >> 8), Expand(value >> 4), Expand(value));
                return true;
            case 6:
                color = FromSrgb8((byte)(value >> 16), (byte)(value >> 8), (byte)value);
                return true;
            case 8:
                color = FromSrgb8((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
                return true;
            default:
                return false;
        }

        // One hex digit stands for itself repeated: #f80 is #ff8800.
        static byte Expand(uint nibble) => (byte)((nibble & 0xF) * 0x11);
    }

    /// <summary>The same colour with a different alpha. RGB is untouched (alpha is straight).</summary>
    public Color WithAlpha(float alpha) => this with { A = alpha };

    /// <summary>Gamma-encoded <c>0xAARRGGBB</c>, clamping each component into range.</summary>
    public uint ToArgb() =>
        ((uint)(byte)MathF.Round(Math.Clamp(A, 0f, 1f) * 255f) << 24)
        | ((uint)SrgbTransfer.ToSrgbByte(R) << 16)
        | ((uint)SrgbTransfer.ToSrgbByte(G) << 8)
        | SrgbTransfer.ToSrgbByte(B);

    /// <summary>Linear-light, straight-alpha components as a <see cref="Vector4"/> (X=R … W=A).</summary>
    public Vector4 ToVector4() => new(R, G, B, A);

    /// <summary>
    /// Linear-light components with RGB multiplied by alpha — the form the GPU blends in and a
    /// render-pass clear value takes.
    /// </summary>
    public Vector4 ToPremultiplied() => new(R * A, G * A, B * A, A);

    /// <summary>
    /// Interpolates in linear light with premultiplied alpha, so fading between an opaque colour
    /// and <see cref="Transparent"/> keeps its hue instead of darkening towards black mid-way.
    /// </summary>
    public static Color Lerp(Color from, Color to, float t)
    {
        var mixed = Vector4.Lerp(from.ToPremultiplied(), to.ToPremultiplied(), t);
        if (mixed.W <= 0f)
        {
            return Transparent;
        }
        return new Color(mixed.X / mixed.W, mixed.Y / mixed.W, mixed.Z / mixed.W, mixed.W);
    }

    /// <summary>Lets a <see cref="Color"/> be passed wherever the renderer takes a <see cref="Vector4"/>.</summary>
    public static implicit operator Vector4(Color color) => color.ToVector4();

    /// <summary>CSS-style <c>#rrggbbaa</c> (gamma-encoded), e.g. <c>#3b82f6ff</c>.</summary>
    public override string ToString()
    {
        var argb = ToArgb();
        return string.Create(CultureInfo.InvariantCulture, $"#{argb & 0xFFFFFF:x6}{argb >> 24:x2}");
    }
}
