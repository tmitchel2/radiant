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
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, text.Length);
        options ??= ShapeOptions.Default;

        using var buffer = new HarfBuzzBuffer();
        buffer.AddUtf16(text, start, length);
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
        for (var i = 0; i < count; i++)
        {
            glyphs[i] = infos[i].Codepoint;
            clusters[i] = (int)infos[i].Cluster;
            advances[i] = positions[i].XAdvance * scale + tracking;
            // HarfBuzz is y-up; the renderer is y-down.
            offsets[i] = new Vector2(positions[i].XOffset * scale, -positions[i].YOffset * scale);
        }
        return new ShapedRun(font, size, options.Direction, start, length, glyphs, clusters, advances, offsets);
    }
}
