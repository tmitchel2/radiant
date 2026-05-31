using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D.Shaders;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D
{
    public unsafe class Renderer2D : IDisposable
    {
        /// <summary>
        /// Default em-square height in logical pixels for UI text (the font's
        /// nominal "1.0" height). All widgets and labels that don't pass an
        /// explicit size scale a multiple of this base, so it is the single
        /// knob for the overall UI text size.
        /// </summary>
        public const float DefaultTextHeightPx = 10f;

        private WebGPU _wgpu = null!;
        private Device* _device;
        private Queue* _queue;
        private RenderPipeline* _filledPipeline;
        private RenderPipeline* _linePipeline;
        private Buffer* _uniformBuffer;
        private BindGroup* _bindGroup;
        private BindGroupLayout* _bindGroupLayout;
        private ShaderModule* _filledShader;
        private ShaderModule* _lineShader;
        private PipelineLayout* _pipelineLayout;

        private Camera2D _camera = null!;
        private readonly List<Vertex2D> _filledVertices = [];
        private readonly List<Vertex2D> _lineVertices = [];

        // MSDF text pipeline (additive — bitmap DrawText is untouched).
        private RenderPipeline* _msdfPipeline;
        private ShaderModule* _msdfShader;
        private BindGroupLayout* _msdfAtlasBindGroupLayout;
        private PipelineLayout* _msdfPipelineLayout;
        private readonly List<MsdfVertex2D> _msdfVertices = [];
        private readonly List<MsdfDrawRange> _msdfRanges = [];
        private readonly List<MsdfFont> _ownedFonts = [];

        // Batched SDF-shape pipeline (rounded rect / disc / ring). Reuses the group-0 uniform layout,
        // no texture. One pipeline draws all analytic shapes, dispatched per-fragment on shape kind.
        private RenderPipeline* _sdfShapePipeline;
        private ShaderModule* _sdfShapeShader;
        private readonly List<SdfShapeVertex2D> _sdfShapeVertices = [];
        private readonly List<SdfShapeDrawRange> _sdfShapeRanges = [];

        internal IReadOnlyList<Vertex2D> FilledVertices => _filledVertices;
        internal IReadOnlyList<Vertex2D> LineVertices => _lineVertices;
        internal IReadOnlyList<MsdfVertex2D> MsdfVertices => _msdfVertices;
        internal IReadOnlyList<SdfShapeVertex2D> SdfShapeVertices => _sdfShapeVertices;
        private TextureFormat _surfaceFormat;
        private readonly List<IntPtr> _frameBuffers = [];

        // Clip/scissor state
        private readonly Stack<ClipRect> _clipStack = new();
        private readonly List<DrawRange> _ranges = [];

        // Scroll-offset state: a translate applied to emitted geometry (not the clip).
        // Markers record the vertex counts at push time; PopScrollOffset shifts everything
        // appended since by the delta, so nested pushes compose cumulatively while the clip
        // viewport stays fixed in window space.
        private readonly Stack<ScrollOffsetMarker> _scrollOffsetStack = new();

        private readonly record struct ScrollOffsetMarker(
            Vector2 Delta, int FilledStart, int LineStart, int MsdfStart, int SdfShapeStart);
        private DrawRange _currentRange;
        private bool _clipEnabled;
        private uint _attachmentWidth;
        private uint _attachmentHeight;
        private float _pixelScale = 1f;

        /// <summary>
        /// Integer pixel rectangle in logical (window) coordinates. Used as the
        /// unit of a clip region; intersections are trivial because rects are
        /// axis-aligned.
        /// </summary>
        public readonly record struct ClipRect(int X, int Y, int Width, int Height)
        {
            public ClipRect Intersect(ClipRect b)
            {
                var x0 = Math.Max(X, b.X);
                var y0 = Math.Max(Y, b.Y);
                var x1 = Math.Min(X + Width, b.X + b.Width);
                var y1 = Math.Min(Y + Height, b.Y + b.Height);
                return new ClipRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
            }
        }

        private struct DrawRange
        {
            public ClipRect? Clip;
            public int FilledStart;
            public int FilledCount;
            public int LineStart;
            public int LineCount;
        }

        private struct MsdfDrawRange
        {
            public MsdfFont Font;
            public ClipRect? Clip;
            public int VertexStart;
            public int VertexCount;
        }

        private struct SdfShapeDrawRange
        {
            public ClipRect? Clip;
            public int VertexStart;
            public int VertexCount;
        }

        public void Initialize(Engine2State engineState, Camera2D camera)
        {
            _wgpu = engineState._wgpu;
            _device = engineState._device;
            _queue = _wgpu.DeviceGetQueue(_device);
            _camera = camera;
            _surfaceFormat = engineState._surfaceCapabilities.Formats[0];

            CreateUniformBuffer();
            CreateBindGroupLayout();
            CreatePipelineLayout();
            CreateShaders();
            CreatePipelines();
            CreateBindGroup();
            CreateMsdfPipeline();
            CreateSdfShapePipeline();
        }

        private void CreateUniformBuffer()
        {
            var bufferDescriptor = new BufferDescriptor
            {
                Size = 64, // 4x4 matrix = 64 bytes
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                MappedAtCreation = false
            };

            _uniformBuffer = _wgpu.DeviceCreateBuffer(_device, in bufferDescriptor);
        }

        private void CreateBindGroupLayout()
        {
            var entry = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Vertex,
                Buffer = new BufferBindingLayout
                {
                    Type = BufferBindingType.Uniform,
                    MinBindingSize = 64
                }
            };

            var descriptor = new BindGroupLayoutDescriptor
            {
                EntryCount = 1,
                Entries = &entry
            };

            _bindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in descriptor);
        }

        private void CreatePipelineLayout()
        {
            var bindGroupLayout = _bindGroupLayout;
            var descriptor = new PipelineLayoutDescriptor
            {
                BindGroupLayoutCount = 1,
                BindGroupLayouts = &bindGroupLayout
            };

            _pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, in descriptor);
        }

        private void CreateShaders()
        {
            _filledShader = CreateShaderModule(ShaderLibrary.FilledShapeShader);
            _lineShader = CreateShaderModule(ShaderLibrary.LineShader);
        }

        private ShaderModule* CreateShaderModule(string code)
        {
            var wgslDescriptor = new ShaderModuleWGSLDescriptor
            {
                Code = (byte*)SilkMarshal.StringToPtr(code),
                Chain = new ChainedStruct
                {
                    SType = SType.ShaderModuleWgslDescriptor
                }
            };

            var shaderModuleDescriptor = new ShaderModuleDescriptor
            {
                NextInChain = (ChainedStruct*)&wgslDescriptor,
            };

            return _wgpu.DeviceCreateShaderModule(_device, in shaderModuleDescriptor);
        }

        private void CreatePipelines()
        {
            _filledPipeline = CreateRenderPipeline(_filledShader, PrimitiveTopology.TriangleList);
            _linePipeline = CreateRenderPipeline(_lineShader, PrimitiveTopology.LineList);
        }

        private RenderPipeline* CreateRenderPipeline(ShaderModule* shader, PrimitiveTopology topology)
        {
            // Vertex buffer layout
            var vertexAttributes = stackalloc VertexAttribute[2];
            vertexAttributes[0] = new VertexAttribute
            {
                Format = VertexFormat.Float32x2, // Position (vec2)
                Offset = 0,
                ShaderLocation = 0
            };
            vertexAttributes[1] = new VertexAttribute
            {
                Format = VertexFormat.Float32x4, // Color (vec4)
                Offset = 8,
                ShaderLocation = 1
            };

            var vertexBufferLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(Vertex2D), // 24 bytes
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 2,
                Attributes = vertexAttributes
            };

            // Blend state
            var blendState = new BlendState
            {
                Color = new BlendComponent
                {
                    SrcFactor = BlendFactor.SrcAlpha,
                    DstFactor = BlendFactor.OneMinusSrcAlpha,
                    Operation = BlendOperation.Add
                },
                Alpha = new BlendComponent
                {
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.Zero,
                    Operation = BlendOperation.Add
                }
            };

            var colorTargetState = new ColorTargetState
            {
                Format = _surfaceFormat,
                Blend = &blendState,
                WriteMask = ColorWriteMask.All
            };

            var fragmentState = new FragmentState
            {
                Module = shader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main")
            };

            var renderPipelineDescriptor = new RenderPipelineDescriptor
            {
                Layout = _pipelineLayout,
                Vertex = new VertexState
                {
                    Module = shader,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                    BufferCount = 1,
                    Buffers = &vertexBufferLayout
                },
                Primitive = new PrimitiveState
                {
                    Topology = topology,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = _camera.Handedness == Handedness.LeftHanded
                        ? FrontFace.CW
                        : FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false
                },
                Fragment = &fragmentState,
                DepthStencil = null
            };

            return _wgpu.DeviceCreateRenderPipeline(_device, in renderPipelineDescriptor);
        }

        private void CreateMsdfPipeline()
        {
            // Bind group 1: sampler + texture (per-font).
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
            var layoutDesc = new BindGroupLayoutDescriptor
            {
                EntryCount = 2,
                Entries = entries,
            };
            _msdfAtlasBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in layoutDesc);

            var layouts = stackalloc BindGroupLayout*[2];
            layouts[0] = _bindGroupLayout;
            layouts[1] = _msdfAtlasBindGroupLayout;
            var pipelineLayoutDesc = new PipelineLayoutDescriptor
            {
                BindGroupLayoutCount = 2,
                BindGroupLayouts = layouts,
            };
            _msdfPipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, in pipelineLayoutDesc);

            _msdfShader = CreateShaderModule(ShaderLibrary.MsdfTextShader);

            var vertexAttributes = stackalloc VertexAttribute[3];
            vertexAttributes[0] = new VertexAttribute
            {
                Format = VertexFormat.Float32x2,
                Offset = 0,
                ShaderLocation = 0,
            };
            vertexAttributes[1] = new VertexAttribute
            {
                Format = VertexFormat.Float32x4,
                Offset = 8,
                ShaderLocation = 1,
            };
            vertexAttributes[2] = new VertexAttribute
            {
                Format = VertexFormat.Float32x2,
                Offset = 24,
                ShaderLocation = 2,
            };

            var vertexBufferLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(MsdfVertex2D),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 3,
                Attributes = vertexAttributes,
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
                Module = _msdfShader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"),
            };

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _msdfPipelineLayout,
                Vertex = new VertexState
                {
                    Module = _msdfShader,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                    BufferCount = 1,
                    Buffers = &vertexBufferLayout,
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = _camera.Handedness == Handedness.LeftHanded
                        ? FrontFace.CW
                        : FrontFace.Ccw,
                    CullMode = CullMode.None,
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false,
                },
                Fragment = &fragmentState,
                DepthStencil = null,
            };

            _msdfPipeline = _wgpu.DeviceCreateRenderPipeline(_device, in pipelineDesc);
        }

        private void CreateSdfShapePipeline()
        {
            _sdfShapeShader = CreateShaderModule(ShaderLibrary.SdfShapeShader);

            // Layout matches SdfShapeVertex2D: position, localPos, color, borderColor, misc, params.
            var vertexAttributes = stackalloc VertexAttribute[6];
            vertexAttributes[0] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 0, ShaderLocation = 0 };
            vertexAttributes[1] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 8, ShaderLocation = 1 };
            vertexAttributes[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 16, ShaderLocation = 2 };
            vertexAttributes[3] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 32, ShaderLocation = 3 };
            vertexAttributes[4] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 48, ShaderLocation = 4 };
            vertexAttributes[5] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 64, ShaderLocation = 5 };

            var vertexBufferLayout = new VertexBufferLayout
            {
                ArrayStride = (ulong)sizeof(SdfShapeVertex2D),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 6,
                Attributes = vertexAttributes,
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
                Module = _sdfShapeShader,
                TargetCount = 1,
                Targets = &colorTargetState,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"),
            };

            // Reuses the base pipeline layout (group 0 = view-projection uniform); no texture bind group.
            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = _pipelineLayout,
                Vertex = new VertexState
                {
                    Module = _sdfShapeShader,
                    EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                    BufferCount = 1,
                    Buffers = &vertexBufferLayout,
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = _camera.Handedness == Handedness.LeftHanded
                        ? FrontFace.CW
                        : FrontFace.Ccw,
                    CullMode = CullMode.None,
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false,
                },
                Fragment = &fragmentState,
                DepthStencil = null,
            };

            _sdfShapePipeline = _wgpu.DeviceCreateRenderPipeline(_device, in pipelineDesc);
        }

        /// <summary>
        /// Register an MSDF font with this renderer. The renderer takes
        /// ownership of the font's GPU resources and disposes them with the
        /// renderer. Must be called after Initialize.
        /// </summary>
        public void RegisterMsdfFont(MsdfFont font)
        {
            if (font.AtlasPngBytes.Length == 0)
            {
                throw new InvalidOperationException("MsdfFont has no atlas PNG bytes loaded.");
            }
            using var img = Image.Load<Rgba32>(font.AtlasPngBytes);
            var width = img.Width;
            var height = img.Height;
            var pixelBytes = new byte[width * height * 4];
            img.CopyPixelDataTo(pixelBytes);
            font.InitializeGpuResources(_wgpu, _device, _queue, _msdfAtlasBindGroupLayout, pixelBytes, width, height);
            _ownedFonts.Add(font);
        }

        private void CreateBindGroup()
        {
            var entry = new BindGroupEntry
            {
                Binding = 0,
                Buffer = _uniformBuffer,
                Offset = 0,
                Size = 64
            };

            var descriptor = new BindGroupDescriptor
            {
                Layout = _bindGroupLayout,
                EntryCount = 1,
                Entries = &entry
            };

            _bindGroup = _wgpu.DeviceCreateBindGroup(_device, in descriptor);
        }

        public void BeginFrame()
        {
            BeginFrame(0, 0, 1f);
        }

        /// <summary>
        /// Starts a new frame with clipping support enabled. Pass the render
        /// attachment size in physical pixels and the logical-to-physical
        /// pixel scale so <see cref="PushClip"/> rectangles can be translated
        /// to a WebGPU scissor rectangle.
        /// </summary>
        public void BeginFrame(uint attachmentWidth, uint attachmentHeight, float pixelScale)
        {
            // Release buffers from previous frame
            foreach (var bufferPtr in _frameBuffers)
            {
                _wgpu.BufferRelease((Buffer*)bufferPtr);
            }
            _frameBuffers.Clear();

            _filledVertices.Clear();
            _lineVertices.Clear();
            _msdfVertices.Clear();
            _msdfRanges.Clear();
            _sdfShapeVertices.Clear();
            _sdfShapeRanges.Clear();
            _clipStack.Clear();
            _scrollOffsetStack.Clear();
            _ranges.Clear();
            _currentRange = new DrawRange { FilledStart = 0, LineStart = 0 };
            _clipEnabled = attachmentWidth > 0 && attachmentHeight > 0;
            _attachmentWidth = attachmentWidth;
            _attachmentHeight = attachmentHeight;
            _pixelScale = pixelScale;
            UpdateUniformBuffer();
        }

        /// <summary>
        /// Pushes a clip rectangle in logical window coordinates. Subsequent
        /// geometry is restricted to the intersection of this rect with the
        /// current clip stack. Must be paired with <see cref="PopClip"/>.
        /// Requires the frame to have been started with the clipping-aware
        /// <see cref="BeginFrame(uint,uint,float)"/> overload.
        /// </summary>
        public void PushClip(float x, float y, float width, float height)
        {
            if (!_clipEnabled) return;
            var newClip = new ClipRect(
                (int)MathF.Floor(x),
                (int)MathF.Floor(y),
                (int)MathF.Ceiling(width),
                (int)MathF.Ceiling(height));
            if (_clipStack.Count > 0)
                newClip = _clipStack.Peek().Intersect(newClip);
            CloseCurrentRange();
            _clipStack.Push(newClip);
        }

        /// <summary>Pops the most recent clip rectangle.</summary>
        public void PopClip()
        {
            if (!_clipEnabled) return;
            if (_clipStack.Count == 0) return;
            CloseCurrentRange();
            _clipStack.Pop();
        }

        /// <summary>
        /// Pushes a scroll translate. Geometry emitted until the matching
        /// <see cref="PopScrollOffset"/> is shifted by <paramref name="delta"/>, while the
        /// clip stack stays in window space (the viewport does not move). Nested pushes
        /// compose cumulatively. Typical use: <c>PushScrollOffset(-controller.Offset)</c>.
        /// </summary>
        public void PushScrollOffset(Vector2 delta) =>
            _scrollOffsetStack.Push(new ScrollOffsetMarker(
                delta,
                _filledVertices.Count,
                _lineVertices.Count,
                _msdfVertices.Count,
                _sdfShapeVertices.Count));

        /// <summary>Pops the most recent scroll translate, shifting geometry emitted since the matching push.</summary>
        public void PopScrollOffset()
        {
            if (_scrollOffsetStack.Count == 0) return;
            var m = _scrollOffsetStack.Pop();
            if (m.Delta == Vector2.Zero) return;

            for (var i = m.FilledStart; i < _filledVertices.Count; i++)
            {
                var v = _filledVertices[i];
                v.Position += m.Delta;
                _filledVertices[i] = v;
            }
            for (var i = m.LineStart; i < _lineVertices.Count; i++)
            {
                var v = _lineVertices[i];
                v.Position += m.Delta;
                _lineVertices[i] = v;
            }
            for (var i = m.MsdfStart; i < _msdfVertices.Count; i++)
            {
                var v = _msdfVertices[i];
                v.Position += m.Delta;
                _msdfVertices[i] = v;
            }
            for (var i = m.SdfShapeStart; i < _sdfShapeVertices.Count; i++)
            {
                var v = _sdfShapeVertices[i];
                v.Position += m.Delta;
                _sdfShapeVertices[i] = v;
            }
        }

        private void CloseCurrentRange()
        {
            _currentRange.FilledCount = _filledVertices.Count - _currentRange.FilledStart;
            _currentRange.LineCount = _lineVertices.Count - _currentRange.LineStart;
            _currentRange.Clip = _clipStack.Count > 0 ? _clipStack.Peek() : null;
            _ranges.Add(_currentRange);
            _currentRange = new DrawRange
            {
                FilledStart = _filledVertices.Count,
                LineStart = _lineVertices.Count,
            };
        }

        private void UpdateUniformBuffer()
        {
            var matrix = _camera.GetProjectionMatrix();
            var matrixData = stackalloc float[16];
            SerializeMatrixForGpu(matrix, new Span<float>(matrixData, 16));
            _wgpu.QueueWriteBuffer(_queue, _uniformBuffer, 0, matrixData, 64);
        }

        /// <summary>
        /// Serializes a System.Numerics Matrix4x4 (row-vector convention) into a flat float buffer
        /// for WGSL consumption (column-vector convention, column-major storage).
        /// Writes M in row-major order so the GPU interprets it as M^T in column-major.
        /// </summary>
        internal static void SerializeMatrixForGpu(Matrix4x4 matrix, Span<float> destination)
        {
            destination[0] = matrix.M11; destination[1] = matrix.M12; destination[2] = matrix.M13; destination[3] = matrix.M14;
            destination[4] = matrix.M21; destination[5] = matrix.M22; destination[6] = matrix.M23; destination[7] = matrix.M24;
            destination[8] = matrix.M31; destination[9] = matrix.M32; destination[10] = matrix.M33; destination[11] = matrix.M34;
            destination[12] = matrix.M41; destination[13] = matrix.M42; destination[14] = matrix.M43; destination[15] = matrix.M44;
        }

        public void DrawRectangleFilled(float x, float y, float width, float height, Vector4 color)
        {
            // Two triangles: (0,1,2) and (0,2,3)
            var v0 = new Vertex2D(new Vector2(x, y), color);
            var v1 = new Vertex2D(new Vector2(x + width, y), color);
            var v2 = new Vertex2D(new Vector2(x + width, y + height), color);
            var v3 = new Vertex2D(new Vector2(x, y + height), color);

            _filledVertices.Add(v0);
            _filledVertices.Add(v1);
            _filledVertices.Add(v2);

            _filledVertices.Add(v0);
            _filledVertices.Add(v2);
            _filledVertices.Add(v3);
        }

        public void DrawRectangleOutline(float x, float y, float width, float height, Vector4 color)
        {
            // Four lines forming a rectangle
            var v0 = new Vertex2D(new Vector2(x, y), color);
            var v1 = new Vertex2D(new Vector2(x + width, y), color);
            var v2 = new Vertex2D(new Vector2(x + width, y + height), color);
            var v3 = new Vertex2D(new Vector2(x, y + height), color);

            _lineVertices.Add(v0); _lineVertices.Add(v1);
            _lineVertices.Add(v1); _lineVertices.Add(v2);
            _lineVertices.Add(v2); _lineVertices.Add(v3);
            _lineVertices.Add(v3); _lineVertices.Add(v0);
        }

        public void DrawCircleFilled(float cx, float cy, float radius, Vector4 color, int segments = 32)
        {
            var center = new Vertex2D(new Vector2(cx, cy), color);

            for (var i = 0; i < segments; i++)
            {
                var angle1 = (i / (float)segments) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)segments) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * radius, cy + MathF.Sin(angle1) * radius),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * radius, cy + MathF.Sin(angle2) * radius),
                    color);

                _filledVertices.Add(center);
                _filledVertices.Add(p1);
                _filledVertices.Add(p2);
            }
        }

        public void DrawCircleOutline(float cx, float cy, float radius, Vector4 color, int segments = 32)
        {
            for (var i = 0; i < segments; i++)
            {
                var angle1 = (i / (float)segments) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)segments) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * radius, cy + MathF.Sin(angle1) * radius),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * radius, cy + MathF.Sin(angle2) * radius),
                    color);

                _lineVertices.Add(p1);
                _lineVertices.Add(p2);
            }
        }

        public void DrawEllipseFilled(float cx, float cy, float rx, float ry, Vector4 color, int segments = 32)
        {
            var center = new Vertex2D(new Vector2(cx, cy), color);

            for (var i = 0; i < segments; i++)
            {
                var angle1 = (i / (float)segments) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)segments) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * rx, cy + MathF.Sin(angle1) * ry),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * rx, cy + MathF.Sin(angle2) * ry),
                    color);

                _filledVertices.Add(center);
                _filledVertices.Add(p1);
                _filledVertices.Add(p2);
            }
        }

        public void DrawEllipseOutline(float cx, float cy, float rx, float ry, Vector4 color, int segments = 32)
        {
            for (var i = 0; i < segments; i++)
            {
                var angle1 = (i / (float)segments) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)segments) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * rx, cy + MathF.Sin(angle1) * ry),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * rx, cy + MathF.Sin(angle2) * ry),
                    color);

                _lineVertices.Add(p1);
                _lineVertices.Add(p2);
            }
        }

        public void DrawLine(Vector2 p1, Vector2 p2, Vector4 color)
        {
            _lineVertices.Add(new Vertex2D(p1, color));
            _lineVertices.Add(new Vertex2D(p2, color));
        }

        public void DrawPolyline(IEnumerable<Vector2> points, Vector4 color)
        {
            Vector2? prevPoint = null;
            foreach (var point in points)
            {
                if (prevPoint.HasValue)
                {
                    _lineVertices.Add(new Vertex2D(prevPoint.Value, color));
                    _lineVertices.Add(new Vertex2D(point, color));
                }
                prevPoint = point;
            }
        }

        public void DrawPolygonFilled(float cx, float cy, float radius, int sides, Vector4 color)
        {
            var center = new Vertex2D(new Vector2(cx, cy), color);

            for (var i = 0; i < sides; i++)
            {
                var angle1 = (i / (float)sides) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)sides) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * radius, cy + MathF.Sin(angle1) * radius),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * radius, cy + MathF.Sin(angle2) * radius),
                    color);

                _filledVertices.Add(center);
                _filledVertices.Add(p1);
                _filledVertices.Add(p2);
            }
        }

        public void DrawPolygonOutline(float cx, float cy, float radius, int sides, Vector4 color)
        {
            for (var i = 0; i < sides; i++)
            {
                var angle1 = (i / (float)sides) * MathF.PI * 2;
                var angle2 = ((i + 1) / (float)sides) * MathF.PI * 2;

                var p1 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle1) * radius, cy + MathF.Sin(angle1) * radius),
                    color);
                var p2 = new Vertex2D(
                    new Vector2(cx + MathF.Cos(angle2) * radius, cy + MathF.Sin(angle2) * radius),
                    color);

                _lineVertices.Add(p1);
                _lineVertices.Add(p2);
            }
        }

        /// <summary>
        /// Draw text using a baked MSDF font. <paramref name="pixelHeight"/>
        /// is the em-square size in pixels — i.e. the font's nominal "1.0"
        /// height. The baseline sits at <paramref name="y"/> + font.AscenderEm
        /// * pixelHeight; the pen origin is <paramref name="x"/>.
        /// </summary>
        public void DrawText(MsdfFont font, string text, float x, float y, float pixelHeight, Vector4 color)
        {
            if (string.IsNullOrEmpty(text)) return;

            var penX = x;
            var baseline = y + font.AscenderEm * pixelHeight;
            var startVertex = _msdfVertices.Count;
            int prev = -1;

            foreach (var rune in text.EnumerateRunes())
            {
                if (!font.TryGetGlyph(rune.Value, out var glyph))
                {
                    penX += 0.25f * pixelHeight;
                    prev = -1;
                    continue;
                }
                if (prev >= 0)
                {
                    penX += font.Kerning(prev, rune.Value) * pixelHeight;
                }
                if (glyph.Width > 0 && glyph.Height > 0)
                {
                    var w = glyph.Width * pixelHeight;
                    var h = glyph.Height * pixelHeight;
                    var x0 = penX + glyph.BearingX * pixelHeight;
                    var y0 = baseline + glyph.BearingY * pixelHeight;
                    var x1 = x0 + w;
                    var y1 = y0 + h;
                    var u0 = glyph.U0;
                    var v0 = glyph.V0;
                    var u1 = glyph.U1;
                    var v1 = glyph.V1;
                    var tl = new MsdfVertex2D(new Vector2(x0, y0), color, new Vector2(u0, v0));
                    var tr = new MsdfVertex2D(new Vector2(x1, y0), color, new Vector2(u1, v0));
                    var bl = new MsdfVertex2D(new Vector2(x0, y1), color, new Vector2(u0, v1));
                    var br = new MsdfVertex2D(new Vector2(x1, y1), color, new Vector2(u1, v1));
                    // Two triangles, CCW: (tl, bl, br) and (tl, br, tr).
                    _msdfVertices.Add(tl);
                    _msdfVertices.Add(bl);
                    _msdfVertices.Add(br);
                    _msdfVertices.Add(tl);
                    _msdfVertices.Add(br);
                    _msdfVertices.Add(tr);
                }
                penX += glyph.Advance * pixelHeight;
                prev = rune.Value;
            }

            var added = _msdfVertices.Count - startVertex;
            if (added == 0) return;

            var clip = _clipStack.Count > 0 ? _clipStack.Peek() : (ClipRect?)null;
            _msdfRanges.Add(new MsdfDrawRange
            {
                Font = font,
                Clip = clip,
                VertexStart = startVertex,
                VertexCount = added,
            });
        }

        /// <summary>Measure pixel width of a string drawn with an MSDF font at the given pixel height.</summary>
        public static float MeasureText(MsdfFont font, string text, float pixelHeight)
            => font.MeasureTextWidth(text, pixelHeight);

        /// <summary>
        /// Draw a single MSDF glyph with its <em>visible bounding box</em> centred
        /// at <paramref name="center"/>. Inverts the pen-origin → glyph-quad
        /// transform that <see cref="DrawText(MsdfFont, string, float, float, float, Vector4)"/>
        /// applies internally, so callers don't have to chase the font's ascender
        /// or each glyph's per-side bearings — useful for icon-in-a-box layouts
        /// (FCF symbol cells, button glyphs, etc.) where the visible centre needs
        /// to align with a known anchor across glyphs from different fonts with
        /// different metrics.
        /// </summary>
        public void DrawGlyphCentered(MsdfFont font, MsdfAtlasGlyph glyph, string text,
            Vector2 center, float pixelHeight, Vector4 color)
        {
            // For DrawText(font, text, pen.X, pen.Y, h, color), the rendered
            // glyph quad lives at:
            //   left = pen.X + glyph.BearingX * h
            //   top  = pen.Y + (font.AscenderEm + glyph.BearingY) * h
            // and is (glyph.Width * h) × (glyph.Height * h) in size. Solving for
            // pen so that the quad centre coincides with `center`:
            var pen = new Vector2(
                center.X - (glyph.BearingX + glyph.Width  * 0.5f) * pixelHeight,
                center.Y - (font.AscenderEm + glyph.BearingY + glyph.Height * 0.5f) * pixelHeight);
            DrawText(font, text, pen.X, pen.Y, pixelHeight, color);
        }

        // ==================== UI Helper Methods ====================

        /// <summary>
        /// Draws MSDF text at a position. <paramref name="pixelHeight"/> is the
        /// font's em-square height in pixels. Default 7f matches the legacy
        /// bitmap-font character height so callers ported from the bitmap path
        /// keep their existing layout pitch.
        /// </summary>
        public void DrawText(MsdfFont font, string text, Vector2 position, Vector4 color, float pixelHeight = DefaultTextHeightPx)
        {
            DrawText(font, text, position.X, position.Y, pixelHeight, color);
        }

        /// <summary>Draws a filled rectangle.</summary>
        public void DrawFilledRect(Vector2 position, Vector2 size, Vector4 color)
        {
            DrawRectangleFilled(position.X, position.Y, size.X, size.Y, color);
        }

        /// <summary>
        /// Draws a filled rounded rectangle via the SDF pipeline (crisp, scale-independent corners
        /// with 1px analytic anti-aliasing). <paramref name="radius"/> is clamped to half the shorter
        /// side; radius 0 yields sharp corners.
        /// </summary>
        public void DrawRoundedRectFilled(float x, float y, float width, float height, float radius, Vector4 color)
            => DrawRoundedRectFilled(x, y, width, height, CornerRadii.All(radius), color);

        /// <summary>
        /// Draws a rounded rectangle with a fill and a border in a single SDF quad. Pass a transparent
        /// <paramref name="fill"/> for a border-only stroke. <paramref name="borderWidth"/> is the
        /// inner stroke width in points; <paramref name="radius"/> is clamped to half the shorter side.
        /// </summary>
        public void DrawRoundedRect(float x, float y, float width, float height, float radius,
            float borderWidth, Vector4 fill, Vector4 border)
            => DrawRoundedRect(x, y, width, height, CornerRadii.All(radius), borderWidth, fill, border);

        /// <summary>Filled rounded rectangle with per-corner radii.</summary>
        public void DrawRoundedRectFilled(float x, float y, float width, float height, CornerRadii radii, Vector4 color)
            => EmitRoundedRect(x, y, width, height, radii, 0f, color, color);

        /// <summary>Rounded rectangle with per-corner radii plus a border.</summary>
        public void DrawRoundedRect(float x, float y, float width, float height, CornerRadii radii,
            float borderWidth, Vector4 fill, Vector4 border)
            => EmitRoundedRect(x, y, width, height, radii, borderWidth, fill, border);

        /// <summary>Vector overload of <see cref="DrawRoundedRectFilled(float,float,float,float,float,Vector4)"/>.</summary>
        public void DrawRoundedRectFilled(Vector2 position, Vector2 size, float radius, Vector4 color)
            => EmitRoundedRect(position.X, position.Y, size.X, size.Y, CornerRadii.All(radius), 0f, color, color);

        /// <summary>Vector overload of <see cref="DrawRoundedRect(float,float,float,float,float,float,Vector4,Vector4)"/>.</summary>
        public void DrawRoundedRect(Vector2 position, Vector2 size, float radius,
            float borderWidth, Vector4 fill, Vector4 border)
            => EmitRoundedRect(position.X, position.Y, size.X, size.Y, CornerRadii.All(radius), borderWidth, fill, border);

        /// <summary>Draws a filled, anti-aliased disc (SDF circle).</summary>
        public void DrawDisc(Vector2 center, float radius, Vector4 color)
            => EmitCircle(center, radius, 0f, 0f, color, color);

        /// <summary>Draws a disc with a fill and an inner border ring.</summary>
        public void DrawDisc(Vector2 center, float radius, float borderWidth, Vector4 fill, Vector4 border)
            => EmitCircle(center, radius, 0f, borderWidth, fill, border);

        /// <summary>
        /// Draws a filled ring/annulus (a crisp circular stroke): the band between
        /// <paramref name="innerRadius"/> and <paramref name="outerRadius"/>. Use for progress rings,
        /// radio outlines, spinners.
        /// </summary>
        public void DrawRing(Vector2 center, float outerRadius, float innerRadius, Vector4 color)
            => EmitCircle(center, outerRadius, MathF.Max(0f, innerRadius), 0f, color, color);

        private void EmitRoundedRect(float x, float y, float width, float height, CornerRadii radii,
            float borderWidth, Vector4 fill, Vector4 border)
        {
            if (width <= 0f || height <= 0f) return;

            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            var maxR = MathF.Min(halfW, halfH);
            var clamped = new Vector4(
                Math.Clamp(radii.TopLeft, 0f, maxR),
                Math.Clamp(radii.TopRight, 0f, maxR),
                Math.Clamp(radii.BottomRight, 0f, maxR),
                Math.Clamp(radii.BottomLeft, 0f, maxR));
            var center = new Vector2(x + halfW, y + halfH);
            EmitShape(center, new Vector2(halfW, halfH), borderWidth, SdfShapeKind.RoundedRect, clamped, fill, border);
        }

        private void EmitCircle(Vector2 center, float outerRadius, float innerRadius,
            float borderWidth, Vector4 fill, Vector4 border)
        {
            if (outerRadius <= 0f) return;
            var half = new Vector2(outerRadius, outerRadius);
            var prms = new Vector4(outerRadius, MathF.Min(innerRadius, outerRadius), 0f, 0f);
            EmitShape(center, half, borderWidth, SdfShapeKind.Circle, prms, fill, border);
        }

        private void EmitShape(Vector2 center, Vector2 halfSize, float borderWidth,
            SdfShapeKind kind, Vector4 prms, Vector4 fill, Vector4 border)
        {
            // Expand the quad by an AA pad so the outer edge fade isn't clipped by the geometry.
            const float aaPad = 1.5f;
            var ext = new Vector2(halfSize.X + aaPad, halfSize.Y + aaPad);
            var misc = new Vector4(halfSize.X, halfSize.Y, borderWidth, (float)(int)kind);

            SdfShapeVertex2D Corner(float sx, float sy)
            {
                var local = new Vector2(sx * ext.X, sy * ext.Y);
                return new SdfShapeVertex2D(center + local, local, fill, border, misc, prms);
            }

            var tl = Corner(-1f, -1f);
            var tr = Corner(1f, -1f);
            var bl = Corner(-1f, 1f);
            var br = Corner(1f, 1f);

            var start = _sdfShapeVertices.Count;
            // Two triangles: (tl, bl, br) and (tl, br, tr) — matches the MSDF quad winding.
            _sdfShapeVertices.Add(tl);
            _sdfShapeVertices.Add(bl);
            _sdfShapeVertices.Add(br);
            _sdfShapeVertices.Add(tl);
            _sdfShapeVertices.Add(br);
            _sdfShapeVertices.Add(tr);

            _sdfShapeRanges.Add(new SdfShapeDrawRange
            {
                Clip = _clipStack.Count > 0 ? _clipStack.Peek() : null,
                VertexStart = start,
                VertexCount = 6,
            });
        }

        /// <summary>Draws a rectangle outline.</summary>
        public void DrawRect(Vector2 position, Vector2 size, Vector4 color)
        {
            DrawRectangleOutline(position.X, position.Y, size.X, size.Y, color);
        }

        /// <summary>Draws a filled circle.</summary>
        public void DrawFilledCircle(Vector2 center, float radius, Vector4 color, int segments = 32)
        {
            DrawCircleFilled(center.X, center.Y, radius, color, segments);
        }

        /// <summary>Draws a circle outline.</summary>
        public void DrawCircle(Vector2 center, float radius, Vector4 color, int segments = 32)
        {
            DrawCircleOutline(center.X, center.Y, radius, color, segments);
        }

        public void EndFrame(RenderPassEncoder* renderPass)
        {
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);

            if (!_clipEnabled)
            {
                // Fast path: draw all vertices in one call each.
                if (_filledVertices.Count > 0)
                {
                    var vertexBuffer = CreateAndUploadVertexBuffer(_filledVertices);
                    _frameBuffers.Add((IntPtr)vertexBuffer);
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _filledPipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, vertexBuffer, 0,
                        (ulong)(_filledVertices.Count * sizeof(Vertex2D)));
                    _wgpu.RenderPassEncoderDraw(renderPass, (uint)_filledVertices.Count, 1, 0, 0);
                }

                if (_lineVertices.Count > 0)
                {
                    var vertexBuffer = CreateAndUploadVertexBuffer(_lineVertices);
                    _frameBuffers.Add((IntPtr)vertexBuffer);
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _linePipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, vertexBuffer, 0,
                        (ulong)(_lineVertices.Count * sizeof(Vertex2D)));
                    _wgpu.RenderPassEncoderDraw(renderPass, (uint)_lineVertices.Count, 1, 0, 0);
                }
                return;
            }

            // Close the final range and emit one draw per range with its scissor.
            CloseCurrentRange();

            Buffer* filledBuffer = null;
            Buffer* lineBuffer = null;
            if (_filledVertices.Count > 0)
            {
                filledBuffer = CreateAndUploadVertexBuffer(_filledVertices);
                _frameBuffers.Add((IntPtr)filledBuffer);
            }
            if (_lineVertices.Count > 0)
            {
                lineBuffer = CreateAndUploadVertexBuffer(_lineVertices);
                _frameBuffers.Add((IntPtr)lineBuffer);
            }

            if (filledBuffer != null)
            {
                _wgpu.RenderPassEncoderSetPipeline(renderPass, _filledPipeline);
                _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, filledBuffer, 0,
                    (ulong)(_filledVertices.Count * sizeof(Vertex2D)));
                foreach (var range in _ranges)
                {
                    if (range.FilledCount == 0) continue;
                    ApplyScissor(renderPass, range.Clip);
                    _wgpu.RenderPassEncoderDraw(renderPass, (uint)range.FilledCount, 1, (uint)range.FilledStart, 0);
                }
            }

            if (lineBuffer != null)
            {
                _wgpu.RenderPassEncoderSetPipeline(renderPass, _linePipeline);
                _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, lineBuffer, 0,
                    (ulong)(_lineVertices.Count * sizeof(Vertex2D)));
                foreach (var range in _ranges)
                {
                    if (range.LineCount == 0) continue;
                    ApplyScissor(renderPass, range.Clip);
                    _wgpu.RenderPassEncoderDraw(renderPass, (uint)range.LineCount, 1, (uint)range.LineStart, 0);
                }
            }

            EmitSdfShapeDraws(renderPass);
            EmitMsdfDraws(renderPass);

            // Restore full-attachment scissor for any subsequent consumer.
            _wgpu.RenderPassEncoderSetScissorRect(renderPass, 0, 0, _attachmentWidth, _attachmentHeight);
        }

        private void EmitMsdfDraws(RenderPassEncoder* renderPass)
        {
            if (_msdfVertices.Count == 0 || _msdfRanges.Count == 0) return;

            var msdfBuffer = CreateAndUploadMsdfVertexBuffer(_msdfVertices);
            _frameBuffers.Add((IntPtr)msdfBuffer);

            _wgpu.RenderPassEncoderSetPipeline(renderPass, _msdfPipeline);
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);
            _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, msdfBuffer, 0,
                (ulong)(_msdfVertices.Count * sizeof(MsdfVertex2D)));

            MsdfFont? boundFont = null;
            foreach (var range in _msdfRanges)
            {
                ApplyScissor(renderPass, range.Clip);
                if (!ReferenceEquals(boundFont, range.Font))
                {
                    _wgpu.RenderPassEncoderSetBindGroup(renderPass, 1, range.Font.BindGroup, 0, null);
                    boundFont = range.Font;
                }
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)range.VertexCount, 1, (uint)range.VertexStart, 0);
            }
        }

        private void EmitSdfShapeDraws(RenderPassEncoder* renderPass)
        {
            if (_sdfShapeVertices.Count == 0 || _sdfShapeRanges.Count == 0) return;

            var buffer = CreateAndUploadSdfShapeVertexBuffer(_sdfShapeVertices);
            _frameBuffers.Add((IntPtr)buffer);

            _wgpu.RenderPassEncoderSetPipeline(renderPass, _sdfShapePipeline);
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);
            _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, buffer, 0,
                (ulong)(_sdfShapeVertices.Count * sizeof(SdfShapeVertex2D)));

            foreach (var range in _sdfShapeRanges)
            {
                ApplyScissor(renderPass, range.Clip);
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)range.VertexCount, 1, (uint)range.VertexStart, 0);
            }
        }

        private Buffer* CreateAndUploadSdfShapeVertexBuffer(List<SdfShapeVertex2D> vertices)
        {
            var bufferDescriptor = new BufferDescriptor
            {
                Size = (ulong)(vertices.Count * sizeof(SdfShapeVertex2D)),
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                MappedAtCreation = false,
            };

            var buffer = _wgpu.DeviceCreateBuffer(_device, in bufferDescriptor);

            var array = vertices.ToArray();
            fixed (SdfShapeVertex2D* dataPtr = array)
            {
                _wgpu.QueueWriteBuffer(_queue, buffer, 0, dataPtr,
                    (nuint)(vertices.Count * sizeof(SdfShapeVertex2D)));
            }

            return buffer;
        }

        private void ApplyScissor(RenderPassEncoder* renderPass, ClipRect? clip)
        {
            if (clip == null)
            {
                _wgpu.RenderPassEncoderSetScissorRect(renderPass, 0, 0, _attachmentWidth, _attachmentHeight);
                return;
            }
            var c = clip.Value;
            var px = (int)MathF.Round(c.X * _pixelScale);
            var py = (int)MathF.Round(c.Y * _pixelScale);
            var pw = (int)MathF.Round(c.Width * _pixelScale);
            var ph = (int)MathF.Round(c.Height * _pixelScale);
            // Clamp into attachment bounds so WebGPU validation is satisfied.
            if (px < 0) { pw += px; px = 0; }
            if (py < 0) { ph += py; py = 0; }
            if (px > (int)_attachmentWidth) px = (int)_attachmentWidth;
            if (py > (int)_attachmentHeight) py = (int)_attachmentHeight;
            if (pw < 0) pw = 0;
            if (ph < 0) ph = 0;
            if (px + pw > (int)_attachmentWidth) pw = (int)_attachmentWidth - px;
            if (py + ph > (int)_attachmentHeight) ph = (int)_attachmentHeight - py;
            _wgpu.RenderPassEncoderSetScissorRect(renderPass, (uint)px, (uint)py, (uint)pw, (uint)ph);
        }

        private Buffer* CreateAndUploadMsdfVertexBuffer(List<MsdfVertex2D> vertices)
        {
            var bufferDescriptor = new BufferDescriptor
            {
                Size = (ulong)(vertices.Count * sizeof(MsdfVertex2D)),
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                MappedAtCreation = false,
            };

            var buffer = _wgpu.DeviceCreateBuffer(_device, in bufferDescriptor);

            var array = vertices.ToArray();
            fixed (MsdfVertex2D* dataPtr = array)
            {
                _wgpu.QueueWriteBuffer(_queue, buffer, 0, dataPtr,
                    (nuint)(vertices.Count * sizeof(MsdfVertex2D)));
            }

            return buffer;
        }

        private Buffer* CreateAndUploadVertexBuffer(List<Vertex2D> vertices)
        {
            var bufferDescriptor = new BufferDescriptor
            {
                Size = (ulong)(vertices.Count * sizeof(Vertex2D)),
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                MappedAtCreation = false
            };

            var buffer = _wgpu.DeviceCreateBuffer(_device, in bufferDescriptor);

            var vertexArray = vertices.ToArray();
            fixed (Vertex2D* dataPtr = vertexArray)
            {
                _wgpu.QueueWriteBuffer(_queue, buffer, 0, dataPtr,
                    (nuint)(vertices.Count * sizeof(Vertex2D)));
            }

            return buffer;
        }

        public void Dispose()
        {
            // Release any remaining frame buffers
            foreach (var bufferPtr in _frameBuffers)
            {
                _wgpu.BufferRelease((Buffer*)bufferPtr);
            }
            _frameBuffers.Clear();

            if (_uniformBuffer != null) _wgpu.BufferRelease(_uniformBuffer);
            if (_bindGroup != null) _wgpu.BindGroupRelease(_bindGroup);
            if (_bindGroupLayout != null) _wgpu.BindGroupLayoutRelease(_bindGroupLayout);
            if (_pipelineLayout != null) _wgpu.PipelineLayoutRelease(_pipelineLayout);
            if (_filledPipeline != null) _wgpu.RenderPipelineRelease(_filledPipeline);
            if (_linePipeline != null) _wgpu.RenderPipelineRelease(_linePipeline);
            if (_filledShader != null) _wgpu.ShaderModuleRelease(_filledShader);
            if (_lineShader != null) _wgpu.ShaderModuleRelease(_lineShader);

            foreach (var font in _ownedFonts) font.Dispose();
            _ownedFonts.Clear();
            if (_msdfPipeline != null) _wgpu.RenderPipelineRelease(_msdfPipeline);
            if (_msdfShader != null) _wgpu.ShaderModuleRelease(_msdfShader);
            if (_msdfPipelineLayout != null) _wgpu.PipelineLayoutRelease(_msdfPipelineLayout);
            if (_msdfAtlasBindGroupLayout != null) _wgpu.BindGroupLayoutRelease(_msdfAtlasBindGroupLayout);
            if (_sdfShapePipeline != null) _wgpu.RenderPipelineRelease(_sdfShapePipeline);
            if (_sdfShapeShader != null) _wgpu.ShaderModuleRelease(_sdfShapeShader);

            GC.SuppressFinalize(this);
        }
    }
}
