using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Radiant.Graphics;
using Radiant.Graphics2D.Shaders;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// Image-blit support for <see cref="Renderer2D"/>: draw an arbitrary GPU texture as a screen-space
    /// quad. Added for the multi-process tab host, which composites a renderer's published frame as a
    /// full-window image with a 2D tab strip on top. Mirrors the MSDF text pipeline (group 0 = the
    /// shared view-projection uniform, group 1 = sampler + texture) and reuses
    /// <see cref="ShaderLibrary.TexturedShader"/>.
    /// </summary>
    public unsafe partial class Renderer2D
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ImageVertex2D
        {
            public Vector2 Position; // offset 0
            public Vector4 Color;    // offset 8
            public Vector2 TexCoord; // offset 24
        }

        private readonly record struct ImageDrawRange(int VertexStart, int VertexCount, IntPtr BindGroup, ClipRect? Clip);

        private RenderPipeline* _imagePipeline;
        private ShaderModule* _imageShader;
        private BindGroupLayout* _imageBindGroupLayout;
        private PipelineLayout* _imagePipelineLayout;
        private readonly List<ImageVertex2D> _imageVertices = [];
        private readonly List<ImageDrawRange> _imageRanges = [];

        /// <summary>
        /// Layout for an image's group-1 bind group (sampler @0 + texture @1). Exposed so callers can
        /// build a <see cref="Texture2D"/> bound to the exact layout this renderer's image pipeline uses.
        /// </summary>
        internal BindGroupLayout* ImageBindGroupLayout => _imageBindGroupLayout;
        internal WebGPU Wgpu => _wgpu;
        internal Device* Device => _device;
        internal Queue* Queue => _queue;

        private void CreateImagePipeline()
        {
            // Group 1: sampler + texture (per-image), identical shape to the MSDF atlas layout.
            var entries = stackalloc BindGroupLayoutEntry[2];
            entries[0] = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Fragment,
                Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
            };
            entries[1] = new BindGroupLayoutEntry
            {
                Binding = 1,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false,
                },
            };
            var layoutDesc = new BindGroupLayoutDescriptor { EntryCount = 2, Entries = entries };
            _imageBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in layoutDesc);

            var layouts = stackalloc BindGroupLayout*[2];
            layouts[0] = _bindGroupLayout; // shared view-projection uniform
            layouts[1] = _imageBindGroupLayout;
            var pipelineLayoutDesc = new PipelineLayoutDescriptor
            {
                BindGroupLayoutCount = 2,
                BindGroupLayouts = layouts,
            };
            _imagePipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, in pipelineLayoutDesc);

            _imageShader = CreateShaderModule(ShaderLibrary.TexturedShader);

            var attrs = stackalloc VertexAttribute[3];
            attrs[0] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 0, ShaderLocation = 0 };
            attrs[1] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 8, ShaderLocation = 1 };
            attrs[2] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 24, ShaderLocation = 2 };
            var vertexBufferLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(ImageVertex2D),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 3,
                Attributes = attrs,
            };

            var blendState = new BlendState
            {
                Color = new BlendComponent
                {
                    SrcFactor = BlendFactor.SrcAlpha,
                    DstFactor = BlendFactor.OneMinusSrcAlpha,
                    Operation = BlendOperation.Add,
                },
                Alpha = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.OneMinusSrcAlpha,
                    Operation = BlendOperation.Add,
                },
            };
            var colorTargetState = new ColorTargetState
            {
                Format = _surfaceFormat,
                Blend = &blendState,
                WriteMask = ColorWriteMask.All,
            };
            var fragmentState = new FragmentState
            {
                Module = _imageShader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"),
            };
            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _imagePipelineLayout,
                Vertex = new VertexState
                {
                    Module = _imageShader,
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
                Multisample = new MultisampleState { Count = 1, Mask = ~0u, AlphaToCoverageEnabled = false },
                Fragment = &fragmentState,
                DepthStencil = null,
            };
            _imagePipeline = _wgpu.DeviceCreateRenderPipeline(_device, in pipelineDesc);
        }

        /// <summary>
        /// Queue an image draw: <paramref name="texture"/> stretched to the rectangle (x, y, width,
        /// height) in screen-space pixels, untinted (opaque white).
        /// Drawn in submission order relative to other image draws; appears under MSDF text.
        /// </summary>
        public void DrawImage(Texture2D texture, float x, float y, float width, float height)
            => DrawImage(texture, x, y, width, height, new Vector4(1, 1, 1, 1));

        public void DrawImage(Texture2D texture, float x, float y, float width, float height, Vector4 tint)
        {
            ArgumentNullException.ThrowIfNull(texture);
            var start = _imageVertices.Count;
            var x0 = x; var y0 = y; var x1 = x + width; var y1 = y + height;

            // Two triangles, UV origin top-left (0,0) → bottom-right (1,1).
            AddImageVertex(x0, y0, 0f, 0f, tint);
            AddImageVertex(x1, y0, 1f, 0f, tint);
            AddImageVertex(x1, y1, 1f, 1f, tint);
            AddImageVertex(x0, y0, 0f, 0f, tint);
            AddImageVertex(x1, y1, 1f, 1f, tint);
            AddImageVertex(x0, y1, 0f, 1f, tint);

            var clip = _clipStack.Count > 0 ? _clipStack.Peek() : (ClipRect?)null;
            _imageRanges.Add(new ImageDrawRange(start, 6, (IntPtr)texture.BindGroup, clip));
        }

        private void AddImageVertex(float x, float y, float u, float v, Vector4 color)
        {
            // Note: image draws don't participate in PushScrollOffset (the tab host doesn't scroll
            // composited frames). If image-in-scroll-region support is ever needed, extend
            // PopScrollOffset to shift _imageVertices like the other vertex lists.
            _imageVertices.Add(new ImageVertex2D { Position = new Vector2(x, y), Color = color, TexCoord = new Vector2(u, v) });
        }

        private void EmitImageDraws(RenderPassEncoder* renderPass)
        {
            if (_imageVertices.Count == 0 || _imageRanges.Count == 0) return;

            var buffer = CreateAndUploadImageVertexBuffer(_imageVertices);
            _frameBuffers.Add((IntPtr)buffer);

            _wgpu.RenderPassEncoderSetPipeline(renderPass, _imagePipeline);
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);
            _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, buffer, 0,
                (ulong)(_imageVertices.Count * sizeof(ImageVertex2D)));

            IntPtr boundGroup = IntPtr.Zero;
            foreach (var range in _imageRanges)
            {
                ApplyScissor(renderPass, range.Clip);
                if (range.BindGroup != boundGroup)
                {
                    _wgpu.RenderPassEncoderSetBindGroup(renderPass, 1, (BindGroup*)range.BindGroup, 0, null);
                    boundGroup = range.BindGroup;
                }
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)range.VertexCount, 1, (uint)range.VertexStart, 0);
            }
        }

        private Buffer* CreateAndUploadImageVertexBuffer(List<ImageVertex2D> vertices)
        {
            var bufferDescriptor = new BufferDescriptor
            {
                Size = (ulong)(vertices.Count * sizeof(ImageVertex2D)),
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                MappedAtCreation = false,
            };
            var buffer = _wgpu.DeviceCreateBuffer(_device, in bufferDescriptor);
            var array = vertices.ToArray();
            fixed (ImageVertex2D* dataPtr = array)
            {
                _wgpu.QueueWriteBuffer(_queue, buffer, 0, dataPtr, (nuint)(array.Length * sizeof(ImageVertex2D)));
            }
            return buffer;
        }

        private void DisposeImageResources()
        {
            if (_imagePipeline != null) _wgpu.RenderPipelineRelease(_imagePipeline);
            if (_imageShader != null) _wgpu.ShaderModuleRelease(_imageShader);
            if (_imagePipelineLayout != null) _wgpu.PipelineLayoutRelease(_imagePipelineLayout);
            if (_imageBindGroupLayout != null) _wgpu.BindGroupLayoutRelease(_imageBindGroupLayout);
            _imagePipeline = null;
            _imageShader = null;
            _imagePipelineLayout = null;
            _imageBindGroupLayout = null;
        }
    }
}
