using System.Collections.Generic;

namespace Radiant.MsdfBaker
{
    public sealed class BakeRequest
    {
        public required string FontPath { get; init; }
        public required string OutputDirectory { get; init; }
        public required string OutputName { get; init; }
        public int GlyphPixelSize { get; init; } = 32;
        public int AtlasSize { get; init; } = 1024;
        public float DistanceRangePx { get; init; } = 4f;
        public IReadOnlyList<int> Codepoints { get; init; } = [];
    }
}
