namespace Radiant.MsdfBaker
{
    /// <summary>
    /// Per-glyph atlas record. UVs are normalised; metrics are in fractions
    /// of the EM unit (1.0 = one line height).
    /// </summary>
    public sealed record AtlasGlyph
    {
        public int Codepoint { get; init; }
        public float U0 { get; init; }
        public float V0 { get; init; }
        public float U1 { get; init; }
        public float V1 { get; init; }
        public float Advance { get; init; }
        public float BearingX { get; init; }
        public float BearingY { get; init; }
        public float Width { get; init; }
        public float Height { get; init; }
    }
}
