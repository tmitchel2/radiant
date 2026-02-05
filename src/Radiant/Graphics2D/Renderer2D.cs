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
        private TextureFormat _surfaceFormat;
        private readonly List<IntPtr> _frameBuffers = [];

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
            // Release buffers from previous frame
            foreach (var bufferPtr in _frameBuffers)
            {
                _wgpu.BufferRelease((Buffer*)bufferPtr);
            }
            _frameBuffers.Clear();

            _filledVertices.Clear();
            _lineVertices.Clear();
            UpdateUniformBuffer();
        }

        private void UpdateUniformBuffer()
        {
            var matrix = _camera.GetProjectionMatrix();
            var matrixData = stackalloc float[16];

            // Matrix4x4 to flat array (column-major for WGSL)
            matrixData[0] = matrix.M11; matrixData[1] = matrix.M21; matrixData[2] = matrix.M31; matrixData[3] = matrix.M41;
            matrixData[4] = matrix.M12; matrixData[5] = matrix.M22; matrixData[6] = matrix.M32; matrixData[7] = matrix.M42;
            matrixData[8] = matrix.M13; matrixData[9] = matrix.M23; matrixData[10] = matrix.M33; matrixData[11] = matrix.M43;
            matrixData[12] = matrix.M14; matrixData[13] = matrix.M24; matrixData[14] = matrix.M34; matrixData[15] = matrix.M44;

            _wgpu.QueueWriteBuffer(_queue, _uniformBuffer, 0, matrixData, 64);
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
                            var py = y - row * pixelSize;
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

            // Draw filled shapes
            if (_filledVertices.Count > 0)
            {
                var vertexBuffer = CreateAndUploadVertexBuffer(_filledVertices);
                _frameBuffers.Add((IntPtr)vertexBuffer);
                _wgpu.RenderPassEncoderSetPipeline(renderPass, _filledPipeline);
                _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, vertexBuffer, 0,
                    (ulong)(_filledVertices.Count * sizeof(Vertex2D)));
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)_filledVertices.Count, 1, 0, 0);
            }

            // Draw lines
            if (_lineVertices.Count > 0)
            {
                var vertexBuffer = CreateAndUploadVertexBuffer(_lineVertices);
                _frameBuffers.Add((IntPtr)vertexBuffer);
                _wgpu.RenderPassEncoderSetPipeline(renderPass, _linePipeline);
                _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, vertexBuffer, 0,
                    (ulong)(_lineVertices.Count * sizeof(Vertex2D)));
                _wgpu.RenderPassEncoderDraw(renderPass, (uint)_lineVertices.Count, 1, 0, 0);
            }
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
