using System.Collections.Generic;

namespace Radiant.MsdfBaker
{
    public sealed class AtlasManifest
    {
        public string FontFamily { get; set; } = "";
        public int AtlasWidth { get; set; }
        public int AtlasHeight { get; set; }
        public int GlyphPixelSize { get; set; }
        public float DistanceRange { get; set; }
        public float LineHeight { get; set; }
        public float Ascender { get; set; }
        public float Descender { get; set; }
        public List<AtlasGlyph> Glyphs { get; set; } = [];
        public List<KerningPair> Kerning { get; set; } = [];
    }

    public sealed record KerningPair(int Left, int Right, float Offset);
}
