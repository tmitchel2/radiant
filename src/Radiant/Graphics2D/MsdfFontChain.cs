using System.Collections.Generic;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// Ordered list of <see cref="MsdfFont"/>s consulted in turn when looking
    /// up a glyph. Lets callers stitch coverage together from multiple atlases
    /// (e.g. Misc Technical + Math Operators + Geometric Shapes) without
    /// requiring a single source font that carries every codepoint.
    /// </summary>
    public sealed class MsdfFontChain
    {
        private readonly List<MsdfFont> _fonts;

        /// <summary>Build a chain from a primary font + ordered fallbacks.</summary>
        public MsdfFontChain(MsdfFont primary, params MsdfFont[] fallbacks)
        {
            _fonts = new List<MsdfFont>(1 + fallbacks.Length) { primary };
            _fonts.AddRange(fallbacks);
        }

        /// <summary>The primary font — used for measure-time metrics when no glyph-specific font is known.</summary>
        public MsdfFont Primary => _fonts[0];

        /// <summary>All fonts in the chain, in lookup order.</summary>
        public IReadOnlyList<MsdfFont> Fonts => _fonts;

        /// <summary>
        /// Append a font as a further fallback. Idempotent on the same instance —
        /// callers can safely call this once at host init without worrying about
        /// duplicates.
        /// </summary>
        public void Append(MsdfFont font)
        {
            if (!_fonts.Contains(font)) _fonts.Add(font);
        }

        /// <summary>
        /// Returns the first font in the chain that has a glyph for
        /// <paramref name="codepoint"/>, along with that font's glyph metrics.
        /// Returns false when no font in the chain covers the codepoint.
        /// </summary>
        public bool TryGetGlyph(int codepoint, out MsdfFont font, out MsdfAtlasGlyph glyph)
        {
            foreach (var f in _fonts)
            {
                if (f.TryGetGlyph(codepoint, out var g) && g.Width > 0f && g.Height > 0f)
                {
                    font = f;
                    glyph = g;
                    return true;
                }
            }
            font = null!;
            glyph = null!;
            return false;
        }
    }
}
