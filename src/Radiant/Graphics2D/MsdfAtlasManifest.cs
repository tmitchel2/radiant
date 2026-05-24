using System.Collections.Generic;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// JSON shape produced by MsdfBaker — kept here in radiant runtime so the
    /// Radiant assembly doesn't take a build-time dependency on the tool. If
    /// the baker JSON schema changes, this type must be updated to match.
    /// </summary>
    public sealed class MsdfAtlasManifest
    {
        public string FontFamily { get; set; } = "";
        public int AtlasWidth { get; set; }
        public int AtlasHeight { get; set; }
        public int GlyphPixelSize { get; set; }
        public float DistanceRange { get; set; }
        public float LineHeight { get; set; }
        public float Ascender { get; set; }
        public float Descender { get; set; }
        public List<MsdfAtlasGlyph> Glyphs { get; set; } = [];
        public List<MsdfKerningPair> Kerning { get; set; } = [];
    }

    public sealed class MsdfAtlasGlyph
    {
        public int Codepoint { get; set; }
        public float U0 { get; set; }
        public float V0 { get; set; }
        public float U1 { get; set; }
        public float V1 { get; set; }
        public float Advance { get; set; }
        public float BearingX { get; set; }
        public float BearingY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public sealed class MsdfKerningPair
    {
        public int Left { get; set; }
        public int Right { get; set; }
        public float Offset { get; set; }
    }
}
