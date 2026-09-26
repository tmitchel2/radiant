using System.Collections.Generic;
using System.Numerics;

namespace Radiant.Text;

/// <summary>How a span of text looks: its font, size, weight, color and typographic features.</summary>
public sealed record TextStyle
{
    /// <summary>Inter at 14 px, regular weight, in opaque black.</summary>
    public static TextStyle Default { get; } = new();

    /// <summary>The font family, as registered in the <see cref="FontLibrary"/>.</summary>
    public string FontFamily { get; init; } = FontLibrary.Inter;

    /// <summary>The size: the em, in logical pixels.</summary>
    public float Size { get; init; } = 14f;

    /// <summary>The weight, 1–1000 (<see cref="Radiant.Text.FontWeight"/> has the common ones).</summary>
    public float Weight { get; init; } = Radiant.Text.FontWeight.Regular;

    /// <summary>Whether to use the family's italic.</summary>
    public bool Italic { get; init; }

    /// <summary>The color, as linear-light straight-alpha RGBA (layout ignores it; drawing uses it).</summary>
    public Vector4 Color { get; init; } = new(0f, 0f, 0f, 1f);

    /// <summary>
    /// The line height in pixels, or null for the font's natural line height. The line box is as
    /// tall as its tallest span needs, with the extra (the leading) split above and below, as CSS
    /// does.
    /// </summary>
    public float? LineHeight { get; init; }

    /// <summary>Extra space after every glyph, in ems: 0.01 is 1% of the size.</summary>
    public float Tracking { get; init; }

    /// <summary>OpenType features to turn on or off, e.g. <see cref="FontFeature.TabularNumbers"/>.</summary>
    public IReadOnlyList<FontFeature> Features { get; init; } = [];

    /// <summary>
    /// Other variable-font axes to set, such as an icon font's fill (<c>FILL</c>). Weight and
    /// optical size come from <see cref="Weight"/> and <see cref="Size"/>.
    /// </summary>
    public IReadOnlyList<FontVariation> Variations { get; init; } = [];

    /// <summary>A BCP 47 language for language-specific forms (Turkish i, Serbian italics), or null.</summary>
    public string? Language { get; init; }
}
