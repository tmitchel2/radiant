using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics2D.Shaders;
using Radiant.Text;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D;

/// <summary>
/// Text laid out by Radiant.Text, drawn from a coverage atlas: each glyph is rasterized at the
/// size it appears on screen, a quarter-pixel horizontal position and a whole-pixel baseline, so
/// small text is as sharp as the pixel grid allows. (MSDF, for text that scales or rotates, is
/// <see cref="DrawText(MsdfFont, string, float, float, float, Vector4)"/>.)
/// </summary>
public unsafe partial class Renderer2D
{
    private const ulong CoverageParamsSize = 16;

    private RenderPipeline* _coveragePipeline;
    private ShaderModule* _coverageShader;
    private Buffer* _coverageParams;
    private GlyphAtlas? _glyphAtlas;
    private readonly List<MsdfVertex2D> _coverageVertices = [];

    internal IReadOnlyList<MsdfVertex2D> CoverageVertices => _coverageVertices;

    internal GlyphAtlas? GlyphAtlas => _glyphAtlas;

    /// <summary>
    /// How <see cref="DrawGlyphRun"/> and <see cref="DrawParagraph"/> draw glyphs. Coverage, the
    /// default, is sharpest at text sizes; see <see cref="Graphics2D.TextRendering"/>.
    /// </summary>
    public TextRendering TextRendering { get; set; } = TextRendering.Coverage;

    /// <summary>
    /// For <see cref="TextRendering.Hybrid"/>: the largest size, in device pixels, drawn from the
    /// coverage atlas. Larger text, and text under a rotation, is drawn with MSDF.
    /// </summary>
    public float HybridThreshold { get; set; } = 24f;

    /// <summary>
    /// The gamma glyph edges are blended as if in, so text has the weight it was designed with
    /// (see the coverage shader): 1 blends edges in linear light as they are, which makes dark
    /// text on a light ground look thin. The default, 1.8, is close to how macOS draws text.
    /// </summary>
    public float TextGamma { get; set; } = 1.8f;

    /// <summary>
    /// Draws a laid-out paragraph with its top left at <paramref name="position"/>, each run in
    /// its style's colour unless <paramref name="color"/> overrides them all.
    /// </summary>
    public void DrawParagraph(Paragraph paragraph, Vector2 position, Vector4? color = null)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        foreach (var line in paragraph.Lines)
        {
            foreach (var run in line.Runs)
            {
                DrawGlyphRun(run, position, color);
            }
        }
    }

    /// <summary>
    /// Draws a run of glyphs from a paragraph, moved by <paramref name="offset"/> (the
    /// paragraph's top left), in the run's colour unless <paramref name="color"/> is given.
    /// <para>
    /// How depends on <see cref="TextRendering"/>. From the coverage atlas, under a transform that
    /// only moves and scales, glyphs snap to the pixel grid: the baseline to a whole pixel, the pen
    /// to a quarter. Under a rotation they are drawn unsnapped, which is softer; text that turns
    /// or zooms is better drawn with MSDF or Slug.
    /// </para>
    /// </summary>
    public void DrawGlyphRun(GlyphRun run, Vector2 offset, Vector4? color = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        var tint = color ?? run.Style.Color;
        var transform = CurrentTransform();
        var mode = TextRendering;
        if (mode == TextRendering.Hybrid)
        {
            var deviceSize = run.Shaped.Size * _pixelScale * MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
            mode = deviceSize <= HybridThreshold && IsAxisAligned(transform) ? TextRendering.Coverage : TextRendering.Msdf;
        }
        switch (mode)
        {
            case TextRendering.Msdf:
                DrawGlyphRunMsdf(run, offset, tint, transform);
                break;
            case TextRendering.Slug:
                DrawGlyphRunSlug(run, offset, tint, transform);
                break;
            default:
                DrawGlyphRunCoverage(run, offset, tint, transform);
                break;
        }
    }

    /// <summary>Whether a transform only moves and scales (positively), so glyphs can snap to pixels.</summary>
    private static bool IsAxisAligned(Matrix3x2 transform) =>
        transform.M12 == 0f && transform.M21 == 0f && transform.M11 > 0f && transform.M22 > 0f;

    private void DrawGlyphRunCoverage(GlyphRun run, Vector2 offset, Vector4 tint, Matrix3x2 transform)
    {
        if (_glyphAtlas is not { } atlas)
        {
            return; // no device: a CPU-only renderer has nowhere to rasterize to
        }

        var shaped = run.Shaped;
        var scale = MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
        var deviceScale = _pixelScale * scale;
        // Sizes are rounded to a sixteenth of a pixel so an animated scale doesn't make a new
        // bitmap every frame.
        var size = MathF.Round(shaped.Size * deviceScale * 16f) / 16f;
        if (size <= 0f || !Matrix3x2.Invert(transform, out var inverse))
        {
            return;
        }
        var snap = IsAxisAligned(transform);

        var pen = run.Origin + offset;
        for (var i = 0; i < shaped.Count; i++)
        {
            var origin = pen + shaped.Offsets[i];
            pen.X += shaped.Advances[i];

            Vector2 topLeft, bottomRight;
            AtlasGlyph glyph;
            if (snap)
            {
                var device = Vector2.Transform(origin, transform) * _pixelScale;
                var x = MathF.Floor(device.X);
                var subpixel = (int)MathF.Round((device.X - x) * 4f);
                if (subpixel == 4)
                {
                    x += 1f;
                    subpixel = 0;
                }
                var baseline = MathF.Round(device.Y);
                glyph = atlas.Get(shaped.Font, shaped.Glyphs[i], size, subpixel);
                if (glyph.IsEmpty)
                {
                    continue;
                }
                // Back from device pixels to the coordinates the transform will be applied to.
                topLeft = Vector2.Transform(new Vector2(x + glyph.Left, baseline + glyph.Top) / _pixelScale, inverse);
                bottomRight = Vector2.Transform(
                    new Vector2(x + glyph.Left + glyph.Width, baseline + glyph.Top + glyph.Height) / _pixelScale, inverse);
            }
            else
            {
                glyph = atlas.Get(shaped.Font, shaped.Glyphs[i], size, 0);
                if (glyph.IsEmpty)
                {
                    continue;
                }
                topLeft = origin + new Vector2(glyph.Left, glyph.Top) / deviceScale;
                bottomRight = topLeft + new Vector2(glyph.Width, glyph.Height) / deviceScale;
            }

            const float texel = 1f / GlyphAtlas.PageSize;
            var uv0 = new Vector2(glyph.X, glyph.Y) * texel;
            var uv1 = new Vector2(glyph.X + glyph.Width, glyph.Y + glyph.Height) * texel;
            var start = _coverageVertices.Count;
            var tl = new MsdfVertex2D(topLeft, tint, uv0);
            var tr = new MsdfVertex2D(new Vector2(bottomRight.X, topLeft.Y), tint, new Vector2(uv1.X, uv0.Y));
            var bl = new MsdfVertex2D(new Vector2(topLeft.X, bottomRight.Y), tint, new Vector2(uv0.X, uv1.Y));
            var br = new MsdfVertex2D(bottomRight, tint, uv1);
            _coverageVertices.Add(tl);
            _coverageVertices.Add(bl);
            _coverageVertices.Add(br);
            _coverageVertices.Add(tl);
            _coverageVertices.Add(br);
            _coverageVertices.Add(tr);
            AppendToBatch(BatchKind.Coverage, start, 6, atlas.BindGroup(glyph.Page));
        }
    }

    /// <summary>
    /// The transform everything drawn now will get: the pushed transforms composed, inner first.
    /// (They're applied to vertices when popped; glyphs need it now, to snap to device pixels.)
    /// </summary>
    private Matrix3x2 CurrentTransform()
    {
        var composed = Matrix3x2.Identity;
        foreach (var marker in _transformStack)
        {
            composed *= marker.Transform;
        }
        return composed;
    }

    private void CreateCoveragePipeline()
    {
        _coverageShader = CreateShaderModule(ShaderLibrary.CoverageTextShader);
        _coveragePipeline = CreateAtlasTextPipeline(_coverageShader);

        var descriptor = new BufferDescriptor
        {
            Size = CoverageParamsSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            MappedAtCreation = false,
        };
        _coverageParams = _wgpu.DeviceCreateBuffer(_device, in descriptor);
        WriteCoverageParams();
        _glyphAtlas = new GlyphAtlas(_wgpu, _device, _queue, _msdfAtlasBindGroupLayout, _coverageParams, CoverageParamsSize);
    }

    private void WriteCoverageParams()
    {
        var values = stackalloc float[4] { MathF.Max(TextGamma, 0.1f), 0f, 0f, 0f };
        _wgpu.QueueWriteBuffer(_queue, _coverageParams, 0, values, (nuint)CoverageParamsSize);
    }

    private void DisposeGlyphResources()
    {
        _glyphAtlas?.Dispose();
        _glyphAtlas = null;
        if (_coverageParams != null) _wgpu.BufferRelease(_coverageParams);
        if (_coveragePipeline != null) _wgpu.RenderPipelineRelease(_coveragePipeline);
        if (_coverageShader != null) _wgpu.ShaderModuleRelease(_coverageShader);
        _coverageParams = null;
        _coveragePipeline = null;
        _coverageShader = null;
    }
}
