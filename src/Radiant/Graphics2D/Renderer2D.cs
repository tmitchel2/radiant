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
    public unsafe partial class Renderer2D : IDisposable
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
        private readonly List<MsdfFont> _ownedFonts = [];

        // Batched SDF-shape pipeline (rounded rect / disc / ring). Reuses the group-0 uniform layout,
        // no texture. One pipeline draws all analytic shapes, dispatched per-fragment on shape kind.
        private RenderPipeline* _sdfShapePipeline;
        private ShaderModule* _sdfShapeShader;
        private readonly List<SdfShapeVertex2D> _sdfShapeVertices = [];

        internal IReadOnlyList<Vertex2D> FilledVertices => _filledVertices;
        internal IReadOnlyList<Vertex2D> LineVertices => _lineVertices;
        internal IReadOnlyList<MsdfVertex2D> MsdfVertices => _msdfVertices;
        internal IReadOnlyList<SdfShapeVertex2D> SdfShapeVertices => _sdfShapeVertices;
        private TextureFormat _surfaceFormat;

        // EVERY PIPELINE BLENDS PREMULTIPLIED ALPHA. The shaders multiply RGB by alpha before they
        // return, so "source over" is One / OneMinusSrcAlpha for colour and alpha alike. Two things
        // follow that straight alpha gets wrong:
        //  - the target's alpha accumulates correctly (two 50% layers cover 75%), which is what a
        //    transparent window or an offscreen frame composited later needs;
        //  - mixing colours inside a shader (an SDF border over its fill, a texel with its
        //    neighbour) stays right when one side is transparent, instead of pulling towards black.
        // Colours passed in stay straight alpha; premultiplying is the shader's job, not the caller's.
        private static readonly BlendState PremultipliedAlphaBlend = new()
        {
            Color = new BlendComponent
            {
                SrcFactor = BlendFactor.One,
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

        // HOW MANY SAMPLES EVERY PIPELINE IS BUILT FOR. A pipeline's sample count must match the
        // attachment it draws into, so this is decided once at Initialize and is the same for all
        // four -- a mismatch is a validation error at draw time rather than a soft failure.
        private uint _sampleCount = 1;
        private readonly List<IntPtr> _frameBuffers = [];

        // Clip/scissor state
        private readonly Stack<ClipRect> _clipStack = new();

        // EVERY DRAW, IN THE ORDER IT WAS MADE. Each primitive kind keeps its own vertex list (and
        // pipeline), but what gets drawn when is decided here: a batch is a run of one kind's
        // vertices with one clip and one texture, and EndFrame replays the batches in order,
        // switching pipeline only where the kind changes. So a popup's background drawn after some
        // text covers that text, whatever pipelines the two use. Consecutive compatible draws extend
        // the last batch, so a frame of many rects is still one draw call.
        private readonly List<DrawBatch> _batches = [];

        // Scroll-offset state: a translate applied to emitted geometry (not the clip).
        // Markers record the vertex counts at push time; PopScrollOffset shifts everything
        // appended since by the delta, so nested pushes compose cumulatively while the clip
        // viewport stays fixed in window space.
        private readonly Stack<ScrollOffsetMarker> _scrollOffsetStack = new();

        private readonly record struct ScrollOffsetMarker(
            Vector2 Delta, int FilledStart, int LineStart, int MsdfStart, int SdfShapeStart, int ImageStart);
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

        // Which pipeline (and vertex list) a batch draws with.
        private enum BatchKind
        {
            Filled,
            Line,
            SdfShape,
            Image,
            Msdf,
        }

        // A run of consecutive vertices of one kind sharing a clip and a group-1 bind group (the
        // font atlas or image; zero for kinds that have none).
        private struct DrawBatch
        {
            public BatchKind Kind;
            public int Start;
            public int Count;
            public ClipRect? Clip;
            public IntPtr BindGroup;
        }

        /// <summary>Number of draw batches recorded this frame. For tests.</summary>
        internal int BatchCount => _batches.Count;

        /// <summary>Builds the pipelines for a target of a given sample count.</summary>
        /// <param name="engineState">The device to build on.</param>
        /// <param name="camera">What the projection comes from.</param>
        /// <param name="sampleCount">
        /// Samples per pixel in the attachment this will draw into. One for no multisampling, which
        /// is the default and what every existing caller gets.
        /// <para>
        /// <b>It belongs on the pipeline, not on the draw call.</b> WebGPU validates a pipeline's
        /// sample count against the attachment's, so the two have to be decided together — and
        /// nothing here is anti-aliased without it: the filled pipeline rasterises hard edges, so a
        /// long straight boundary is a staircase and a curve is a staircase that happens to look
        /// like a curve.
        /// </para>
        /// </param>
        public void Initialize(Engine2State engineState, Camera2D camera, uint sampleCount = 1)
        {
            _sampleCount = Math.Max(1u, sampleCount);

            _wgpu = engineState._wgpu;
            _device = engineState._device;
            _queue = _wgpu.DeviceGetQueue(_device);
            _camera = camera;
            _surfaceFormat = SurfaceFormats.ChooseColorFormat(engineState._surfaceCapabilities);

            CreateUniformBuffer();
            CreateBindGroupLayout();
            CreatePipelineLayout();
            CreateShaders();
            CreatePipelines();
            CreateBindGroup();
            CreateMsdfPipeline();
            CreateSdfShapePipeline();
            CreateImagePipeline();
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
            var blendState = PremultipliedAlphaBlend;

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
                    Count = _sampleCount,
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
            // Bind group 1 (per font): sampler, atlas texture, and the atlas parameters uniform.
            var entries = stackalloc BindGroupLayoutEntry[3];
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
            entries[2] = new BindGroupLayoutEntry
            {
                Binding = 2,
                Visibility = ShaderStage.Fragment,
                Buffer = new BufferBindingLayout
                {
                    Type = BufferBindingType.Uniform,
                    MinBindingSize = MsdfFont.AtlasParamsSize,
                },
            };
            var layoutDesc = new BindGroupLayoutDescriptor
            {
                EntryCount = 3,
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

            var blendState = PremultipliedAlphaBlend;

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
                    Count = _sampleCount,
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

            var blendState = PremultipliedAlphaBlend;

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
                    Count = _sampleCount,
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
        /// renderer. Must be called after Initialize. Optional: DrawText
        /// registers a font it has not seen on first use.
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
            _sdfShapeVertices.Clear();
            _imageVertices.Clear();
            _batches.Clear();
            _clipStack.Clear();
            _scrollOffsetStack.Clear();
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
            _clipStack.Push(newClip);
        }

        /// <summary>Pops the most recent clip rectangle.</summary>
        public void PopClip()
        {
            if (!_clipEnabled) return;
            if (_clipStack.Count == 0) return;
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
                _sdfShapeVertices.Count,
                _imageVertices.Count));

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
            for (var i = m.ImageStart; i < _imageVertices.Count; i++)
            {
                var v = _imageVertices[i];
                v.Position += m.Delta;
                _imageVertices[i] = v;
            }
        }

        private void AddFilled(Vertex2D vertex)
        {
            AppendToBatch(BatchKind.Filled, _filledVertices.Count, 1, IntPtr.Zero);
            _filledVertices.Add(vertex);
        }

        private void AddLine(Vertex2D vertex)
        {
            AppendToBatch(BatchKind.Line, _lineVertices.Count, 1, IntPtr.Zero);
            _lineVertices.Add(vertex);
        }

        // Records that `count` vertices starting at `start` of `kind`'s list are drawn next: they
        // extend the last batch if it is the same kind, clip and bind group and they follow on from
        // it, and start a new batch otherwise.
        private void AppendToBatch(BatchKind kind, int start, int count, IntPtr bindGroup)
        {
            ClipRect? clip = _clipStack.Count > 0 ? _clipStack.Peek() : null;
            if (_batches.Count > 0)
            {
                var last = _batches[^1];
                if (last.Kind == kind && last.BindGroup == bindGroup && last.Clip == clip && last.Start + last.Count == start)
                {
                    last.Count += count;
                    _batches[^1] = last;
                    return;
                }
            }
            _batches.Add(new DrawBatch { Kind = kind, Start = start, Count = count, Clip = clip, BindGroup = bindGroup });
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

            AddFilled(v0);
            AddFilled(v1);
            AddFilled(v2);

            AddFilled(v0);
            AddFilled(v2);
            AddFilled(v3);
        }

        // A line with a width, as two triangles. WebGPU has no lineWidth -- LineList is always one
        // physical pixel -- so anything wider than a hairline has to be geometry. Uses the plain
        // filled pipeline rather than the SDF one because EmitShape only emits an axis-aligned quad
        // around a centre, and a segment at an arbitrary angle is not that.
        //
        // Butt caps: the quad ends exactly at p1 and p2. A chain of these therefore leaves a notch
        // on the outside of a corner, which is what a caller joining segments should expect and
        // handle (a disc at each joint is the usual answer).
        public void DrawThickLine(Vector2 p1, Vector2 p2, float width, Vector4 color)
        {
            var along = p2 - p1;
            var length = along.Length();

            if (length <= float.Epsilon || width <= 0f)
            {
                // A zero-length segment has no direction to be perpendicular to. Degenerate rather
                // than an error: callers draw what their data says, and a board with two pads at
                // one point is a board, not a crash.
                return;
            }

            var across = new Vector2(-along.Y, along.X) / length * (width * 0.5f);

            DrawQuad(p1 + across, p2 + across, p2 - across, p1 - across, color);
        }

        /// <summary>A width-carrying polyline, with the caps and joins a chain of quads has not got.</summary>
        /// <param name="points">The path, in order. Consecutive duplicates are ignored.</param>
        /// <param name="width">How wide the stroke is.</param>
        /// <param name="color">What colour.</param>
        /// <param name="cap">How the two free ends are finished.</param>
        /// <param name="join">How the outside of each corner is filled.</param>
        /// <param name="miterLimit">
        /// How many half-widths a miter spike may reach before it becomes a bevel. Four is the
        /// usual default and is what SVG and Canvas use.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>DrawThickLine per segment is not this.</b> Each quad ends square at the shared point,
        /// so the outside of every bend is left with a wedge missing and the path reads as a chain
        /// of separate rectangles rather than one stroke. This emits the joins that close it.
        /// </para>
        /// <para>
        /// <b>Everything goes through the same filled pipeline as the segments</b>, deliberately. A
        /// round join drawn with the anti-aliased SDF disc would be blended against a non-anti-aliased
        /// quad and show a seam at every joint, which is a worse artefact than the notch it fixed.
        /// </para>
        /// </remarks>
        public void DrawThickPolyline(
            IReadOnlyList<Vector2> points,
            float width,
            Vector4 color,
            LineCap cap = LineCap.Butt,
            LineJoin join = LineJoin.Miter,
            float miterLimit = 4f)
        {
            ArgumentNullException.ThrowIfNull(points);

            if (width <= 0f)
            {
                return;
            }

            var path = Distinct(points);

            if (path.Count == 1)
            {
                // A zero-length path is still a place, and a round cap on one is a dot. Canvas-style
                // rasterisers draw it; returning nothing here would lose a track whose two ends are
                // the same point, which is a real thing on a real board.
                if (cap == LineCap.Round)
                {
                    // FOUR QUARTER TURNS. Two half turns is the same trap the caps fell into: at
                    // exactly pi both ways round are "shortest", so both halves can come out the
                    // same one and the dot is a half-moon with a hole where the rest should be.
                    Dot(path[0], width * 0.5f, color);
                }

                return;
            }

            if (path.Count < 2)
            {
                return;
            }

            var half = width * 0.5f;

            // A RING HAS NO ENDS, AND ITS SEAM IS A JOIN RATHER THAN TWO CAPS. Distinct only removes
            // CONSECUTIVE duplicates, so a caller closing a loop by repeating the first point leaves
            // it in -- and then the join loop skips that point, two caps are stamped on top of each
            // other at arbitrary orientations, and the seam notches under every style. The
            // convention is the one a caller can already see: first point equal to last.
            var closed = path.Count >= 4 && path[0] == path[^1];

            if (closed)
            {
                path.RemoveAt(path.Count - 1);
            }

            var last = closed ? path.Count : path.Count - 1;

            for (var at = 0; at < last; at++)
            {
                var to = path[(at + 1) % path.Count];
                var along = Vector2.Normalize(to - path[at]);
                var across = new Vector2(-along.Y, along.X) * half;

                DrawQuad(
                    path[at] + across, to + across, to - across, path[at] - across, color);
            }

            for (var at = closed ? 0 : 1; at < (closed ? path.Count : path.Count - 1); at++)
            {
                EmitJoin(
                    path[((at - 1) + path.Count) % path.Count],
                    path[at],
                    path[(at + 1) % path.Count],
                    half,
                    color,
                    join,
                    miterLimit);
            }

            if (closed)
            {
                return;
            }

            EmitCap(path[0], Vector2.Normalize(path[0] - path[1]), half, color, cap);
            EmitCap(path[^1], Vector2.Normalize(path[^1] - path[^2]), half, color, cap);
        }

        // Consecutive duplicates carry no direction, so a join or a cap taken from one would
        // normalise a zero vector and produce NaN geometry -- which renders as nothing at all, or as
        // a triangle stretching across the whole viewport.
        private static List<Vector2> Distinct(IReadOnlyList<Vector2> points)
        {
            var path = new List<Vector2>(points.Count);

            foreach (var point in points)
            {
                if (path.Count == 0 || path[^1] != point)
                {
                    path.Add(point);
                }
            }

            return path;
        }

        // The outside of one corner. `before`, `at` and `after` are consecutive path points.
        private void EmitJoin(
            Vector2 before,
            Vector2 at,
            Vector2 after,
            float half,
            Vector4 color,
            LineJoin join,
            float miterLimit)
        {
            var into = Vector2.Normalize(at - before);
            var outOf = Vector2.Normalize(after - at);

            var turn = (into.X * outOf.Y) - (into.Y * outOf.X);

            if (turn == 0f)
            {
                // Straight through needs no join. Doubled back is different: the stroke's own outline
                // turns around there, which under a round join is a half-disc on each side -- so a
                // full one, and it is the case a hairpin actually produces.
                if (join == LineJoin.Round && Vector2.Dot(into, outOf) < 0f)
                {
                    Dot(at, half, color);
                }

                return;
            }

            // WHICH SIDE IS THE OUTSIDE. A left turn opens the gap on one side and a right turn on
            // the other, and filling the wrong one leaves the notch exactly where it was while
            // painting over copper that was already covered.
            var side = turn > 0f ? -1f : 1f;

            var first = at + (new Vector2(-into.Y, into.X) * half * side);
            var second = at + (new Vector2(-outOf.Y, outOf.X) * half * side);

            switch (join)
            {
                case LineJoin.Bevel:
                    DrawTriangle(at, first, second, color);

                    break;

                case LineJoin.Round:
                    Arc(at, first, second, color);

                    break;

                case LineJoin.Miter:
                default:
                    var sum = Vector2.Normalize(first - at) + Vector2.Normalize(second - at);
                    var length = sum.Length();

                    // A REAL TOLERANCE, NOT float.Epsilon. That constant is 1.4e-45, so the guard
                    // read as "exactly zero" and left a band where sum is tiny, Normalize overflows,
                    // and `reach` comes out NaN -- which fails the limit test below, because every
                    // comparison against NaN is false, and puts NaN into the vertex buffer.
                    if (length <= 1e-6f)
                    {
                        DrawTriangle(at, first, second, color);

                        break;
                    }

                    var direction = sum / length;
                    var reach = half / Vector2.Dot(direction, Vector2.Normalize(first - at));

                    // Never shorter than the stroke, so a limit below one is geometrically
                    // impossible; a non-finite one clamps to always-bevel, which is the safe way.
                    var limit = float.IsFinite(miterLimit) ? MathF.Max(1f, miterLimit) : 1f;

                    // PAST THE LIMIT IT BECOMES A BEVEL, which is standard and is not cosmetic: the
                    // spike grows as half/sin(theta/2) and is unbounded as the corner sharpens, so a
                    // hairpin would throw a needle across the viewport. This is SVG's rule exactly --
                    // miterLength / strokeWidth > stroke-miterlimit.
                    if (!float.IsFinite(reach) || reach > half * limit)
                    {
                        DrawTriangle(at, first, second, color);

                        break;
                    }

                    DrawQuad(at, first, at + (direction * reach), second, color);

                    break;
            }
        }

        private void EmitCap(Vector2 at, Vector2 outward, float half, Vector4 color, LineCap cap)
        {
            var across = new Vector2(-outward.Y, outward.X) * half;

            switch (cap)
            {
                case LineCap.Square:
                    DrawQuad(
                        at + across,
                        at + across + (outward * half),
                        at - across + (outward * half),
                        at - across,
                        color);

                    break;

                case LineCap.Round:
                    // TWO QUARTER TURNS THROUGH THE TIP, not one half turn from edge to edge. A cap
                    // is exactly pi, which is the one sweep where "the short way round" has no
                    // answer -- and the arbitrary choice went outward at one end of a line and
                    // inward at the other, so one cap drew over copper already there and the other
                    // drew the cap. Going via the tip states which half is meant.
                    var tip = at + (outward * half);

                    Arc(at, at + across, tip, color);
                    Arc(at, tip, at - across, color);

                    break;

                case LineCap.Butt:
                default:
                    break;
            }
        }

        // A whole disc, as four unambiguous quarter turns.
        private void Dot(Vector2 centre, float radius, Vector4 color)
        {
            var right = new Vector2(radius, 0f);
            var down = new Vector2(0f, radius);

            Arc(centre, centre + right, centre + down, color);
            Arc(centre, centre + down, centre - right, color);
            Arc(centre, centre - right, centre - down, color);
            Arc(centre, centre - down, centre + right, color);
        }

        // A fan from `centre` sweeping the short way from `from` to `to`, both of which are the same
        // distance out. Used for a round join and for half of a round cap -- and a cap is the 180
        // degree case, which is why the sweep is taken as an angle rather than as a turn direction.
        private void Arc(Vector2 centre, Vector2 from, Vector2 to, Vector4 color)
        {
            var start = MathF.Atan2(from.Y - centre.Y, from.X - centre.X);
            var end = MathF.Atan2(to.Y - centre.Y, to.X - centre.X);
            var radius = (from - centre).Length();

            var sweep = end - start;

            while (sweep > MathF.PI)
            {
                sweep -= MathF.Tau;
            }

            while (sweep < -MathF.PI)
            {
                sweep += MathF.Tau;
            }

            // Callers must not hand this a half turn: at exactly pi the two ways round are both
            // "shortest" and they cover different halves. EmitCap therefore goes via the tip.
            // SUBDIVIDED BY HOW FAR THE CHORD SAGS, not by a fixed angle. A fixed step makes a
            // hairline pay for eight triangles it cannot show and a very wide stroke look polygonal;
            // this keeps the error under a quarter of a pixel at any radius.
            const float Flatness = 0.25f;

            var steps = radius <= Flatness
                ? 1
                : Math.Clamp(
                    (int)MathF.Ceiling(MathF.Abs(sweep) / MathF.Acos(1f - (Flatness / radius))),
                    1,
                    32);
            var step = sweep / steps;
            var previous = from;

            for (var index = 1; index <= steps; index++)
            {
                var angle = start + (step * index);
                var next = centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);

                DrawTriangle(centre, previous, next, color);
                previous = next;
            }
        }

        // An arbitrary quad, wound a-b-c-d. Convex is assumed: a self-intersecting or reflex quad
        // renders as its two triangles, which is the standard fan artefact rather than a fix
        // anything here could apply. CullMode is None, so the winding direction does not matter.
        public void DrawQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 color)
        {
            DrawTriangle(a, b, c, color);
            DrawTriangle(a, c, d, color);
        }

        public void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Vector4 color)
        {
            AddFilled(new Vertex2D(a, color));
            AddFilled(new Vertex2D(b, color));
            AddFilled(new Vertex2D(c, color));
        }

        public void DrawRectangleOutline(float x, float y, float width, float height, Vector4 color)
        {
            // Four lines forming a rectangle
            var v0 = new Vertex2D(new Vector2(x, y), color);
            var v1 = new Vertex2D(new Vector2(x + width, y), color);
            var v2 = new Vertex2D(new Vector2(x + width, y + height), color);
            var v3 = new Vertex2D(new Vector2(x, y + height), color);

            AddLine(v0); AddLine(v1);
            AddLine(v1); AddLine(v2);
            AddLine(v2); AddLine(v3);
            AddLine(v3); AddLine(v0);
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

                AddFilled(center);
                AddFilled(p1);
                AddFilled(p2);
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

                AddLine(p1);
                AddLine(p2);
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

                AddFilled(center);
                AddFilled(p1);
                AddFilled(p2);
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

                AddLine(p1);
                AddLine(p2);
            }
        }

        public void DrawLine(Vector2 p1, Vector2 p2, Vector4 color)
        {
            AddLine(new Vertex2D(p1, color));
            AddLine(new Vertex2D(p2, color));
        }

        public void DrawPolyline(IEnumerable<Vector2> points, Vector4 color)
        {
            Vector2? prevPoint = null;
            foreach (var point in points)
            {
                if (prevPoint.HasValue)
                {
                    AddLine(new Vertex2D(prevPoint.Value, color));
                    AddLine(new Vertex2D(point, color));
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

                AddFilled(center);
                AddFilled(p1);
                AddFilled(p2);
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

                AddLine(p1);
                AddLine(p2);
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

            // An unregistered font has no bind group, and binding a null group aborts the process inside
            // wgpu. RadiantApplication creates the renderer inside Run, out of the caller's reach, so the
            // first draw is where a font can be registered. (No device means a CPU-only test renderer
            // that never submits, so there is nothing to register with.)
            if (font.BindGroup == null && _device != null)
            {
                RegisterMsdfFont(font);
            }

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

            AppendToBatch(BatchKind.Msdf, startVertex, added, (IntPtr)font.BindGroup);
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

            AppendToBatch(BatchKind.SdfShape, start, 6, IntPtr.Zero);
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

        /// <summary>
        /// Encodes the frame's draws into <paramref name="renderPass"/>, in the order they were made.
        /// </summary>
        public void EndFrame(RenderPassEncoder* renderPass)
        {
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);
            if (_batches.Count == 0)
            {
                return;
            }

            // One vertex buffer per kind, uploaded once; batches index into them.
            Buffer* filledBuffer = UploadIfAny(_filledVertices);
            Buffer* lineBuffer = UploadIfAny(_lineVertices);
            Buffer* sdfShapeBuffer = UploadIfAny(_sdfShapeVertices);
            Buffer* imageBuffer = UploadIfAny(_imageVertices);
            Buffer* msdfBuffer = UploadIfAny(_msdfVertices);

            BatchKind? boundKind = null;
            var boundGroup = IntPtr.Zero;
            ClipRect? appliedClip = null;
            var scissorApplied = false;
            foreach (var batch in _batches)
            {
                if (boundKind != batch.Kind)
                {
                    BindPipeline(renderPass, batch.Kind, filledBuffer, lineBuffer, sdfShapeBuffer, imageBuffer, msdfBuffer);
                    boundKind = batch.Kind;
                    boundGroup = IntPtr.Zero;
                }
                if (batch.BindGroup != IntPtr.Zero && batch.BindGroup != boundGroup)
                {
                    _wgpu.RenderPassEncoderSetBindGroup(renderPass, 1, (BindGroup*)batch.BindGroup, 0, null);
                    boundGroup = batch.BindGroup;
                }
                if (_clipEnabled && (!scissorApplied || appliedClip != batch.Clip))
                {
                    ApplyScissor(renderPass, batch.Clip);
                    appliedClip = batch.Clip;
                    scissorApplied = true;
                }
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)batch.Count, 1, (uint)batch.Start, 0);
            }

            if (_clipEnabled)
            {
                // Restore full-attachment scissor for any subsequent consumer.
                _wgpu.RenderPassEncoderSetScissorRect(renderPass, 0, 0, _attachmentWidth, _attachmentHeight);
            }
        }

        private void BindPipeline(
            RenderPassEncoder* renderPass, BatchKind kind,
            Buffer* filledBuffer, Buffer* lineBuffer, Buffer* sdfShapeBuffer, Buffer* imageBuffer, Buffer* msdfBuffer)
        {
            switch (kind)
            {
                case BatchKind.Filled:
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _filledPipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, filledBuffer, 0, (ulong)(_filledVertices.Count * sizeof(Vertex2D)));
                    break;
                case BatchKind.Line:
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _linePipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, lineBuffer, 0, (ulong)(_lineVertices.Count * sizeof(Vertex2D)));
                    break;
                case BatchKind.SdfShape:
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _sdfShapePipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, sdfShapeBuffer, 0, (ulong)(_sdfShapeVertices.Count * sizeof(SdfShapeVertex2D)));
                    break;
                case BatchKind.Image:
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _imagePipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, imageBuffer, 0, (ulong)(_imageVertices.Count * sizeof(ImageVertex2D)));
                    break;
                case BatchKind.Msdf:
                    _wgpu.RenderPassEncoderSetPipeline(renderPass, _msdfPipeline);
                    _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, msdfBuffer, 0, (ulong)(_msdfVertices.Count * sizeof(MsdfVertex2D)));
                    break;
            }
        }

        // Uploads a vertex list into a buffer released at the next BeginFrame, or null if it is empty.
        private Buffer* UploadIfAny<T>(List<T> vertices) where T : unmanaged
        {
            if (vertices.Count == 0)
            {
                return null;
            }
            var bytes = (ulong)(vertices.Count * sizeof(T));
            var descriptor = new BufferDescriptor
            {
                Size = bytes,
                Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                MappedAtCreation = false,
            };
            var buffer = _wgpu.DeviceCreateBuffer(_device, in descriptor);
            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices);
            fixed (T* data = span)
            {
                _wgpu.QueueWriteBuffer(_queue, buffer, 0, data, (nuint)bytes);
            }
            _frameBuffers.Add((IntPtr)buffer);
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

            DisposeImageResources();

            GC.SuppressFinalize(this);
        }
    }
}
