using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D.Shaders;
using Radiant.Text;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D;

/// <summary>
/// Text drawn with Slug (Eric Lengyel, "GPU-Centered Font Rendering Directly from Glyph Outlines",
/// JCGT 2017; patent dedicated to the public domain, reference shaders MIT): each glyph is one quad,
/// and the fragment shader works out every pixel's coverage from the glyph's quadratic curves. There
/// is no bitmap at any size, so text stays exact under any scale or rotation, at a higher cost per
/// pixel than sampling an atlas.
/// <para>
/// Glyphs are prepared once per font instance and glyph by Radiant.Text.Slug and kept in storage
/// buffers (<see cref="SlugGlyphCache"/>). Quads are made in the coordinates they are drawn in, so
/// <see cref="PopTransform"/> moves them like any other vertex; each corner carries its em-space
/// position, which the transform leaves alone.
/// </para>
/// </summary>
public unsafe partial class Renderer2D
{
    private RenderPipeline* _slugPipeline;
    private ShaderModule* _slugShader;
    private BindGroupLayout* _slugBindGroupLayout;
    private PipelineLayout* _slugPipelineLayout;
    private SlugGlyphCache? _slugGlyphs;
    private Buffer* _slugVertexBuffer;
    private readonly List<SlugVertex2D> _slugVertices = [];

    internal IReadOnlyList<SlugVertex2D> SlugVertices => _slugVertices;

    internal SlugGlyphCache? SlugGlyphs => _slugGlyphs;

    /// <summary>
    /// Draws a glyph run with Slug: one quad per glyph at the run's size, grown on screen by half a
    /// pixel or a little more (the paper's dilation; see <see cref="TryDilation"/>) so the pixels
    /// its edges partly cover are all shaded, whatever the transform.
    /// </summary>
    private void DrawGlyphRunSlug(GlyphRun run, Vector2 offset, Vector4 tint, Matrix3x2 transform)
    {
        if (_slugGlyphs is not { } cache)
        {
            return; // no device: a CPU-only renderer has nothing to evaluate outlines on
        }
        var shaped = run.Shaped;
        var size = shaped.Size;
        if (size <= 0f || !TryDilation(transform, out var dilation))
        {
            return;
        }
        var emDilation = dilation / size;

        var pen = run.Origin + offset;
        for (var i = 0; i < shaped.Count; i++)
        {
            var origin = pen + shaped.Offsets[i];
            pen.X += shaped.Advances[i];
            var glyph = cache.Get(shaped.Font, shaped.Glyphs[i]);
            if (glyph.IsEmpty)
            {
                continue;
            }

            // The glyph's box, from em units (y up) to the run's coordinates (y down), grown.
            var left = origin.X + (glyph.Min.X * size) - dilation.X;
            var right = origin.X + (glyph.Max.X * size) + dilation.X;
            var top = origin.Y - (glyph.Max.Y * size) - dilation.Y;
            var bottom = origin.Y - (glyph.Min.Y * size) + dilation.Y;
            var emLeft = glyph.Min.X - emDilation.X;
            var emRight = glyph.Max.X + emDilation.X;
            var emTop = glyph.Max.Y + emDilation.Y;
            var emBottom = glyph.Min.Y - emDilation.Y;

            var start = _slugVertices.Count;
            var tl = new SlugVertex2D(new Vector2(left, top), new Vector2(emLeft, emTop), tint, glyph);
            var tr = new SlugVertex2D(new Vector2(right, top), new Vector2(emRight, emTop), tint, glyph);
            var bl = new SlugVertex2D(new Vector2(left, bottom), new Vector2(emLeft, emBottom), tint, glyph);
            var br = new SlugVertex2D(new Vector2(right, bottom), new Vector2(emRight, emBottom), tint, glyph);
            _slugVertices.Add(tl);
            _slugVertices.Add(bl);
            _slugVertices.Add(br);
            _slugVertices.Add(tl);
            _slugVertices.Add(br);
            _slugVertices.Add(tr);
            AppendToBatch(BatchKind.Slug, start, 6, IntPtr.Zero);
        }
    }

    /// <summary>
    /// How far to grow a glyph's quad, in the coordinates it is drawn in, so that on screen every
    /// edge moves out by half a pixel's extent across it: every pixel whose square touches the glyph
    /// has its centre inside the quad and is shaded.
    /// <para>
    /// The paper grows the box by half a pixel; the reference vertex shader solves for that per
    /// vertex under a perspective projection. Radiant's transforms are affine and known when the
    /// run is drawn (they are applied at <see cref="PopTransform"/>), so the solve has a closed form.
    /// With <c>A</c> the linear part from these coordinates to device pixels, and <c>ax</c> and
    /// <c>ay</c> where it takes the x and y axes, moving the vertical edges out by <c>dx</c> moves
    /// them on screen by <c>|det A| dx / |ay|</c>, and a pixel square reaches
    /// <c>(|n.x| + |n.y|) / 2</c> across an edge of unit normal <c>n</c>. So
    /// <c>dx = (|ay.x| + |ay.y|) / (2 |det A|)</c>, and <c>dy</c> likewise with <c>ax</c>: half a
    /// pixel on an unrotated edge, up to 0.71 of one at 45°, where the square reaches furthest.
    /// </para>
    /// <para>
    /// That is also exactly where Slug's own anti-aliasing ends: the shader measures a pixel along
    /// each em axis by <c>fwidth</c>, which is the same sum of absolute values, and a crossing more
    /// than half of that away contributes nothing. So no pixel with coverage is left out, and the
    /// quad reaches no further than it must.
    /// </para>
    /// </summary>
    private bool TryDilation(Matrix3x2 transform, out Vector2 dilation)
    {
        // Row vectors: the x axis maps to (M11, M12) and the y axis to (M21, M22), then device
        // pixels are logical units times the pixel scale.
        var s = _pixelScale;
        var det = MathF.Abs(transform.GetDeterminant()) * s * s;
        if (!(det > 1e-12f) || !float.IsFinite(det))
        {
            dilation = default;
            return false;
        }
        var xAxis = (MathF.Abs(transform.M11) + MathF.Abs(transform.M12)) * s;
        var yAxis = (MathF.Abs(transform.M21) + MathF.Abs(transform.M22)) * s;
        dilation = new Vector2(yAxis, xAxis) * (0.5f / det);
        return true;
    }

    private void CreateSlugPipeline()
    {
        // Group 1: the curve and band storage buffers and the parameters, all read by the fragment stage.
        var entries = stackalloc BindGroupLayoutEntry[3];
        entries[0] = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Fragment,
            Buffer = new BufferBindingLayout { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 16 },
        };
        entries[1] = new BindGroupLayoutEntry
        {
            Binding = 1,
            Visibility = ShaderStage.Fragment,
            Buffer = new BufferBindingLayout { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 4 },
        };
        entries[2] = new BindGroupLayoutEntry
        {
            Binding = 2,
            Visibility = ShaderStage.Fragment,
            Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform, MinBindingSize = SlugGlyphCache.ParamsSize },
        };
        var layoutDescriptor = new BindGroupLayoutDescriptor { EntryCount = 3, Entries = entries };
        _slugBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in layoutDescriptor);

        var layouts = stackalloc BindGroupLayout*[2];
        layouts[0] = _bindGroupLayout;
        layouts[1] = _slugBindGroupLayout;
        var pipelineLayoutDescriptor = new PipelineLayoutDescriptor { BindGroupLayoutCount = 2, BindGroupLayouts = layouts };
        _slugPipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, in pipelineLayoutDescriptor);

        _slugShader = CreateShaderModule(ShaderLibrary.SlugTextShader);

        // SlugVertex2D: position, em, colour, (band base, curve base).
        var attributes = stackalloc VertexAttribute[4];
        attributes[0] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 0, ShaderLocation = 0 };
        attributes[1] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 8, ShaderLocation = 1 };
        attributes[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 16, ShaderLocation = 2 };
        attributes[3] = new VertexAttribute { Format = VertexFormat.Uint32x2, Offset = 32, ShaderLocation = 3 };
        var vertexBufferLayout = new VertexBufferLayout
        {
            ArrayStride = (ulong)sizeof(SlugVertex2D),
            StepMode = VertexStepMode.Vertex,
            AttributeCount = 4,
            Attributes = attributes,
        };

        var blendState = PremultipliedAlphaBlend;
        var colorTargetState = new ColorTargetState { Format = _surfaceFormat, Blend = &blendState, WriteMask = ColorWriteMask.All };
        var fragmentState = new FragmentState
        {
            Module = _slugShader,
            TargetCount = 1,
            Targets = &colorTargetState,
            EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"),
        };
        var pipelineDescriptor = new RenderPipelineDescriptor
        {
            Layout = _slugPipelineLayout,
            Vertex = new VertexState
            {
                Module = _slugShader,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                BufferCount = 1,
                Buffers = &vertexBufferLayout,
            },
            Primitive = new PrimitiveState
            {
                Topology = PrimitiveTopology.TriangleList,
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = _camera.Handedness == Handedness.LeftHanded ? FrontFace.CW : FrontFace.Ccw,
                CullMode = CullMode.None,
            },
            Multisample = new MultisampleState { Count = _sampleCount, Mask = ~0u, AlphaToCoverageEnabled = false },
            Fragment = &fragmentState,
            DepthStencil = null,
        };
        _slugPipeline = _wgpu.DeviceCreateRenderPipeline(_device, in pipelineDescriptor);
        _slugGlyphs = new SlugGlyphCache(_wgpu, _device, _queue, _slugBindGroupLayout);
    }

    /// <summary>At the start of a frame: forgets the last frame's quads, and empties the glyph cache if it is full.</summary>
    private void BeginSlugFrame()
    {
        _slugVertices.Clear();
        _slugGlyphs?.TrimIfFull();
    }

    /// <summary>At the end of a frame, before any batch is replayed: uploads the quads and any newly prepared glyphs.</summary>
    private void UploadSlug()
    {
        _slugVertexBuffer = UploadIfAny(BatchKind.Slug, _slugVertices);
        if (_slugVertices.Count > 0)
        {
            _slugGlyphs?.Upload(MathF.Max(TextGamma, 0.1f));
        }
    }

    /// <summary>
    /// Binds the Slug pipeline, its quads and the glyph buffers. The buffers are bound here rather
    /// than per batch, because growing them at the end of the frame replaces the bind group.
    /// </summary>
    private void BindSlugPipeline(RenderPassEncoder* renderPass)
    {
        _wgpu.RenderPassEncoderSetPipeline(renderPass, _slugPipeline);
        _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, _slugVertexBuffer, 0, (ulong)(_slugVertices.Count * sizeof(SlugVertex2D)));
        _wgpu.RenderPassEncoderSetBindGroup(renderPass, 1, _slugGlyphs!.BindGroup, 0, null);
    }

    /// <summary>Moves the quads drawn since a transform was pushed; their em coordinates stay.</summary>
    private void TransformSlugVertices(int start, Matrix3x2 transform)
    {
        for (var i = start; i < _slugVertices.Count; i++)
        {
            var v = _slugVertices[i];
            v.Position = Vector2.Transform(v.Position, transform);
            _slugVertices[i] = v;
        }
    }

    private void DisposeSlugResources()
    {
        _slugGlyphs?.Dispose();
        _slugGlyphs = null;
        if (_slugPipeline != null) _wgpu.RenderPipelineRelease(_slugPipeline);
        if (_slugShader != null) _wgpu.ShaderModuleRelease(_slugShader);
        if (_slugPipelineLayout != null) _wgpu.PipelineLayoutRelease(_slugPipelineLayout);
        if (_slugBindGroupLayout != null) _wgpu.BindGroupLayoutRelease(_slugBindGroupLayout);
        _slugPipeline = null;
        _slugShader = null;
        _slugPipelineLayout = null;
        _slugBindGroupLayout = null;
        _slugVertexBuffer = null;
    }
}
