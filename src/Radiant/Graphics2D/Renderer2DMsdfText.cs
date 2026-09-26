using System.Numerics;
using Radiant.Text;

namespace Radiant.Graphics2D;

/// <summary>
/// Laid-out text drawn from multi-channel signed distance fields generated at runtime
/// (<see cref="MsdfGlyphAtlas"/>): each glyph's field is made once and drawn at any size, scale or
/// rotation through the MSDF pipeline, which works out the edge's sharpness per pixel.
/// </summary>
public unsafe partial class Renderer2D
{
    private MsdfGlyphAtlas? _msdfGlyphAtlas;

    /// <summary>The runtime MSDF atlas, once text has been drawn with it. For tests.</summary>
    internal MsdfGlyphAtlas? MsdfGlyphAtlas => _msdfGlyphAtlas;

    /// <summary>The page size the MSDF atlas is made with, when first used. Tests fill small pages quickly.</summary>
    internal int MsdfAtlasPageSize { get; set; } = MsdfGlyphAtlas.DefaultPageSize;

    /// <summary>
    /// Draws a glyph run with MSDF glyphs generated at runtime. Quads are placed in the run's own
    /// coordinates, unsnapped, scaled from the atlas's em to the run's size; the transforms in
    /// force are applied to them when popped, as to everything else, and the shader keeps the
    /// edges one pixel wide whatever they come to.
    /// </summary>
    private void DrawGlyphRunMsdf(GlyphRun run, Vector2 offset, Vector4 tint, Matrix3x2 transform)
    {
        if (_device == null || transform.GetDeterminant() == 0f)
        {
            return; // no device (a CPU-only renderer has nowhere to upload to), or nothing to see
        }
        var atlas = _msdfGlyphAtlas ??= new MsdfGlyphAtlas(_wgpu, _device, _queue, _msdfAtlasBindGroupLayout, MsdfAtlasPageSize);

        var shaped = run.Shaped;
        if (shaped.Size <= 0f)
        {
            return;
        }
        // Local units per atlas texel.
        var scale = shaped.Size / MsdfGlyphAtlas.EmSize;
        atlas.Prepare(shaped.Font, shaped.Glyphs);

        var pen = run.Origin + offset;
        for (var i = 0; i < shaped.Count; i++)
        {
            var origin = pen + shaped.Offsets[i];
            pen.X += shaped.Advances[i];
            var glyph = atlas.Get(shaped.Font, shaped.Glyphs[i]);
            if (glyph.IsEmpty)
            {
                continue;
            }
            var topLeft = origin + new Vector2(glyph.Left, glyph.Top) * scale;
            var bottomRight = topLeft + new Vector2(glyph.Width, glyph.Height) * scale;

            var texel = 1f / atlas.PageSize;
            var uv0 = new Vector2(glyph.X, glyph.Y) * texel;
            var uv1 = new Vector2(glyph.X + glyph.Width, glyph.Y + glyph.Height) * texel;
            var start = _msdfVertices.Count;
            var tl = new MsdfVertex2D(topLeft, tint, uv0);
            var tr = new MsdfVertex2D(new Vector2(bottomRight.X, topLeft.Y), tint, new Vector2(uv1.X, uv0.Y));
            var bl = new MsdfVertex2D(new Vector2(topLeft.X, bottomRight.Y), tint, new Vector2(uv0.X, uv1.Y));
            var br = new MsdfVertex2D(bottomRight, tint, uv1);
            _msdfVertices.Add(tl);
            _msdfVertices.Add(bl);
            _msdfVertices.Add(br);
            _msdfVertices.Add(tl);
            _msdfVertices.Add(br);
            _msdfVertices.Add(tr);
            AppendToBatch(BatchKind.Msdf, start, 6, atlas.BindGroup(glyph.Page));
        }
    }

    private void DisposeMsdfTextResources()
    {
        _msdfGlyphAtlas?.Dispose();
        _msdfGlyphAtlas = null;
    }
}
