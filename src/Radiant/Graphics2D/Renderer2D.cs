using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D.Shaders;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D
{
    public unsafe class Renderer2D : IDisposable
    {
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

        internal IReadOnlyList<Vertex2D> FilledVertices => _filledVertices;
        internal IReadOnlyList<Vertex2D> LineVertices => _lineVertices;
        private TextureFormat _surfaceFormat;
        private readonly List<IntPtr> _frameBuffers = [];

        // Clip/scissor state
        private readonly Stack<ClipRect> _clipStack = new();
        private readonly List<DrawRange> _ranges = [];
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
            _clipStack.Clear();
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

        public void DrawText(string text, float x, float y, float pixelSize, Vector4 color)
        {
            var cursorX = x;

            foreach (var c in text)
            {
                if (!BitmapFont.HasGlyph(c))
                {
                    cursorX += (BitmapFont.CharWidth + BitmapFont.CharSpacing) * pixelSize;
                    continue;
                }

                var glyph = BitmapFont.GetGlyph(c);

                // Draw each pixel of the glyph
                for (var row = 0; row < BitmapFont.CharHeight; row++)
                {
                    for (var col = 0; col < BitmapFont.CharWidth; col++)
                    {
                        var bit = (glyph[row] >> (BitmapFont.CharWidth - 1 - col)) & 1;
                        if (bit == 1)
                        {
                            var px = cursorX + col * pixelSize;
                            var py = y + row * pixelSize;
                            DrawRectangleFilled(px, py, pixelSize, pixelSize, color);
                        }
                    }
                }

                cursorX += (BitmapFont.CharWidth + BitmapFont.CharSpacing) * pixelSize;
            }
        }

        // ==================== UI Helper Methods ====================

        /// <summary>Draws text at a position with default pixel size of 1.</summary>
        public void DrawText(string text, Vector2 position, Vector4 color, float scale = 1f)
        {
            DrawText(text, position.X, position.Y, scale, color);
        }

        /// <summary>Draws a filled rectangle.</summary>
        public void DrawFilledRect(Vector2 position, Vector2 size, Vector4 color)
        {
            DrawRectangleFilled(position.X, position.Y, size.X, size.Y, color);
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

            // Restore full-attachment scissor for any subsequent consumer.
            _wgpu.RenderPassEncoderSetScissorRect(renderPass, 0, 0, _attachmentWidth, _attachmentHeight);
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

            GC.SuppressFinalize(this);
        }
    }
}
