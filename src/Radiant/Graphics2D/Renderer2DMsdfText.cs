using System.Numerics;
using Radiant.Text;

namespace Radiant.Graphics2D;

public partial class Renderer2D
{
    /// <summary>Draws a glyph run with MSDF glyphs generated at runtime. Until that lands, from the coverage atlas.</summary>
    private void DrawGlyphRunMsdf(GlyphRun run, Vector2 offset, Vector4 tint, Matrix3x2 transform) =>
        DrawGlyphRunCoverage(run, offset, tint, transform);
}
