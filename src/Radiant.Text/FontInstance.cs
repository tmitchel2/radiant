using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HarfBuzzSharp;

namespace Radiant.Text;

/// <summary>
/// A <see cref="FontFace"/> at fixed variation values: what text is shaped and glyphs are drawn
/// with. Sizes are applied afterwards, as a scale, so one instance serves every size. (Optical size
/// is a variation, so a face with an <c>opsz</c> axis gets an instance per optical size.)
/// </summary>
public sealed class FontInstance : IDisposable
{
    private readonly HarfBuzzSharp.Font _font;
    private readonly ConcurrentDictionary<uint, GlyphOutline> _outlines = new();
    private readonly FontMetrics _unitMetrics;

    internal FontInstance(FontFace face, IReadOnlyList<FontVariation> variations)
    {
        Face = face;
        Variations = variations;
        _font = new HarfBuzzSharp.Font(face.Face);
        // Work in font units: positions come back exact, and a size is a single multiply later.
        _font.SetScale(face.UnitsPerEm, face.UnitsPerEm);
        if (variations.Count > 0)
        {
            _font.SetVariations(variations.Select(v => new Variation { Tag = Tag.Parse(v.Tag), Value = v.Value }).ToArray());
        }
        // Immutable, a HarfBuzz font can be shaped with from several threads at once.
        HarfBuzzDraw.MakeImmutable(_font.Handle);

        _font.TryGetHorizontalFontExtents(out var extents);
        _font.OpenTypeMetrics.TryGetPosition(OpenTypeMetricsTag.CapHeight, out var capHeight);
        _font.OpenTypeMetrics.TryGetPosition(OpenTypeMetricsTag.XHeight, out var xHeight);
        _unitMetrics = new FontMetrics(extents.Ascender, -extents.Descender, extents.LineGap, capHeight, xHeight);
    }

    /// <summary>The face this is an instance of.</summary>
    public FontFace Face { get; }

    /// <summary>The variation values applied, clamped to the face's axes.</summary>
    public IReadOnlyList<FontVariation> Variations { get; }

    internal HarfBuzzSharp.Font HarfBuzzFont => _font;

    /// <summary>The scale from font units to pixels at a size (the em in pixels).</summary>
    public float Scale(float size) => size / Face.UnitsPerEm;

    /// <summary>Vertical metrics at a size, in pixels.</summary>
    public FontMetrics Metrics(float size)
    {
        var s = Scale(size);
        return new FontMetrics(
            _unitMetrics.Ascender * s, _unitMetrics.Descender * s, _unitMetrics.LineGap * s,
            _unitMetrics.CapHeight * s, _unitMetrics.XHeight * s);
    }

    /// <summary>The glyph for a code point, or false if the font has none.</summary>
    public bool TryGetGlyph(int codePoint, out uint glyph) => _font.TryGetGlyph((uint)codePoint, out glyph);

    /// <summary>A glyph's advance at a size, in pixels, before shaping adjusts it.</summary>
    public float GetAdvance(uint glyph, float size) => _font.GetHorizontalGlyphAdvance(glyph) * Scale(size);

    /// <summary>
    /// A glyph's outline, in font units with y up, at this instance's variation values. Cached:
    /// outlines don't depend on size.
    /// </summary>
    public GlyphOutline GetOutline(uint glyph) => _outlines.GetOrAdd(glyph, g => HarfBuzzDraw.Draw(_font.Handle, g));

    public void Dispose() => _font.Dispose();

    /// <summary>For example <c>Inter [opsz=14, wght=600]</c>.</summary>
    public override string ToString() =>
        $"{Face.FamilyName}{(Face.IsItalic ? " Italic" : "")}" +
        (Variations.Count == 0 ? "" : $" [{string.Join(", ", Variations.Select(v => $"{v.Tag}={v.Value:0.##}"))}]");
}
