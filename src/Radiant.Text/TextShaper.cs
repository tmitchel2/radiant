using System;
using System.Numerics;
using HarfBuzzSharp;
using HarfBuzzBuffer = HarfBuzzSharp.Buffer;

namespace Radiant.Text;

/// <summary>
/// Turns text into positioned glyphs with HarfBuzz: ligatures, kerning, mark placement, contextual
/// forms, and complex scripts. Shape one run at a time, one direction and one font per run;
/// splitting text into runs is paragraph layout's job.
/// </summary>
public static class TextShaper
{
    /// <summary>Shapes all of <paramref name="text"/> as one run.</summary>
    public static ShapedRun Shape(string text, FontInstance font, float size, ShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Shape(text, 0, text.Length, font, size, options);
    }

    /// <summary>
    /// Shapes <paramref name="length"/> code units of <paramref name="text"/> from
    /// <paramref name="start"/>. The rest of the text is context: a run is shaped knowing what
    /// comes before and after it, so Arabic letters join across a style change. Clusters index into
    /// the whole text.
    /// </summary>
    public static ShapedRun Shape(string text, int start, int length, FontInstance font, float size, ShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Shape(text, start, length, 0, text.Length, font, size, options);
    }

    /// <summary>
    /// Shapes a run seeing only <c>text[contextStart..contextEnd)</c> around it: paragraph layout
    /// shapes the part of a run on one line without the next line's text, so an Arabic letter
    /// before a line break takes its final form.
    /// </summary>
    internal static ShapedRun Shape(string text, int start, int length, int contextStart, int contextEnd,
        FontInstance font, float size, ShapeOptions? options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, text.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(contextStart, start);
        ArgumentOutOfRangeException.ThrowIfLessThan(contextEnd, start + length);
        options ??= ShapeOptions.Default;

        using var buffer = new HarfBuzzBuffer();
        buffer.AddUtf16(text.AsSpan(contextStart, contextEnd - contextStart), start - contextStart, length);
        buffer.GuessSegmentProperties();
        buffer.Direction = options.Direction == TextDirection.RightToLeft ? Direction.RightToLeft : Direction.LeftToRight;
        if (options.Script is { } script)
        {
            buffer.Script = Script.Parse(script);
        }
        if (options.Language is { } language)
        {
            buffer.Language = new Language(language);
        }

        var features = new Feature[options.Features.Count];
        for (var i = 0; i < features.Length; i++)
        {
            features[i] = new Feature(Tag.Parse(options.Features[i].Tag), (uint)options.Features[i].Value);
        }
        font.HarfBuzzFont.Shape(buffer, features);

        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var count = infos.Length;
        var scale = font.Scale(size);
        var tracking = options.Tracking * size;
        var glyphs = new uint[count];
        var clusters = new int[count];
        var advances = new float[count];
        var offsets = new Vector2[count];
        var unsafeToBreak = new bool[count];
        uint? space = null;
        for (var i = 0; i < count; i++)
        {
            glyphs[i] = infos[i].Codepoint;
            clusters[i] = (int)infos[i].Cluster + contextStart;
            advances[i] = positions[i].XAdvance * scale + tracking;
            // HarfBuzz is y-up; the renderer is y-down.
            offsets[i] = new Vector2(positions[i].XOffset * scale, -positions[i].YOffset * scale);
            unsafeToBreak[i] = (infos[i].GlyphFlags & GlyphFlags.UnsafeToBreak) != 0;

            // Fonts have no glyph for a newline or other control character, so HarfBuzz would give
            // the missing-glyph box. They draw nothing: a blank glyph with no advance, except a tab,
            // which advances to a width set in spaces (tab stops are a later refinement).
            var unit = text[clusters[i]];
            if (char.IsControl(unit) || unit is '\u2028' or '\u2029')
            {
                space ??= font.TryGetGlyph(' ', out var s) ? s : 0u;
                glyphs[i] = space.Value;
                offsets[i] = Vector2.Zero;
                advances[i] = unit == '\t' ? font.GetAdvance(space.Value, size) * options.TabSize : 0f;
            }
        }
        return new ShapedRun(font, size, options.Direction, start, length, glyphs, clusters, advances, offsets, unsafeToBreak);
    }
}
