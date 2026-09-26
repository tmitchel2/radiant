using System;
using System.Collections.Generic;
using SixLabors.Fonts;

namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Finds the kerning between every pair of baked glyphs by laying each pair out twice — once with
    /// kerning, once without — and keeping the difference in the first glyph's advance.
    /// <para>
    /// <b>Why measure rather than read a table.</b> Modern fonts (Inter among them) kern through
    /// OpenType GPOS class-based lookups rather than the legacy <c>kern</c> table, and SixLabors applies
    /// those only during layout. Measuring through the layout engine therefore gets whatever the font
    /// actually specifies. Laying each pair out both ways isolates kerning from everything else layout
    /// does. A pair that shapes into anything other than those same two codepoints is skipped, because
    /// the runtime draws the two glyphs separately: a ligature such as <c>-&gt;</c> into an arrow is one
    /// example.
    /// </para>
    /// </summary>
    public static class KerningExtractor
    {
        /// <summary>Offsets smaller than this fraction of an em are dropped as noise.</summary>
        public const float MinimumOffsetEm = 0.001f;

        /// <summary>
        /// Kerning pairs among <paramref name="codepoints"/>, as offsets in ems added to the first
        /// glyph's advance (negative pulls the pair together).
        /// </summary>
        public static List<KerningPair> Extract(Font font, IReadOnlyList<int> codepoints, float emSize)
        {
            var kerned = new TextOptions(font) { KerningMode = KerningMode.Standard };
            var unkerned = new TextOptions(font) { KerningMode = KerningMode.None };
            var pairs = new List<KerningPair>();
            Span<char> buffer = stackalloc char[4];

            foreach (var left in codepoints)
            {
                foreach (var right in codepoints)
                {
                    var length = Encode(left, buffer);
                    length += Encode(right, buffer[length..]);
                    var text = buffer[..length];

                    if (!TryFirstAdvance(text, kerned, left, right, out var withKerning)
                        || !TryFirstAdvance(text, unkerned, left, right, out var withoutKerning))
                    {
                        continue;
                    }

                    var offsetEm = (withKerning - withoutKerning) / emSize;
                    if (MathF.Abs(offsetEm) >= MinimumOffsetEm)
                    {
                        pairs.Add(new KerningPair(left, right, offsetEm));
                    }
                }
            }

            return pairs;
        }

        // The advance of the pair's first glyph, or false when the pair did not shape into exactly
        // the two codepoints asked for (a ligature or other substitution the runtime cannot reproduce).
        private static bool TryFirstAdvance(ReadOnlySpan<char> text, TextOptions options, int left, int right, out float advance)
        {
            advance = 0f;
            if (!TextMeasurer.TryMeasureCharacterAdvances(text, options, out var advances)
                || advances.Length != 2
                || advances[0].Codepoint.Value != left
                || advances[1].Codepoint.Value != right)
            {
                return false;
            }

            advance = advances[0].Bounds.Width;
            return true;
        }

        private static int Encode(int codepoint, Span<char> destination) =>
            new System.Text.Rune(codepoint).EncodeToUtf16(destination);
    }
}
