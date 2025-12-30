using System;
using System.Numerics;
using Radiant;
using RadiantCli.Graphics2D;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace RadiantCli.Commands
{
    public unsafe class LinesCommand : ICommand
    {
        public void Execute(string[] args)
        {
            var options = WindowOptions.Default;
            options.API = GraphicsAPI.None;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "Radiant - Lines Demo";
            options.IsVisible = true;
            options.ShouldSwapAutomatically = false;

            var window = Window.Create(options);

            Engine2State? engineState = null;
            Renderer2D? renderer = null;
            Camera2D? camera = null;

            window.Load += () =>
            {
                engineState = InitializeEngine(window);
                camera = new Camera2D(800, 600);
                renderer = new Renderer2D();
                renderer.Initialize(engineState, camera);
            };

            window.Closing += () =>
            {
                renderer?.Dispose();
                CleanupEngine(engineState!);
            };

            window.FramebufferResize += (size) =>
            {
                if (engineState != null && camera != null)
                {
                    CreateSwapchain(engineState);
                    camera.SetViewportSize(size.X, size.Y);
                }
            };

            window.Render += (delta) =>
            {
                if (engineState == null || renderer == null) return;

                SurfaceTexture surfaceTexture;
                engineState._wgpu.SurfaceGetCurrentTexture(engineState._surface, &surfaceTexture);

                switch (surfaceTexture.Status)
                {
                    case SurfaceGetCurrentTextureStatus.Timeout:
                    case SurfaceGetCurrentTextureStatus.Outdated:
                    case SurfaceGetCurrentTextureStatus.Lost:
                        engineState._wgpu.TextureRelease(surfaceTexture.Texture);
                        CreateSwapchain(engineState);
                        return;
                    case SurfaceGetCurrentTextureStatus.OutOfMemory:
                    case SurfaceGetCurrentTextureStatus.DeviceLost:
                        throw new Exception($"Surface error: {surfaceTexture.Status}");
                }

                var view = engineState._wgpu.TextureCreateView(surfaceTexture.Texture, null);
                var commandEncoderDescriptor = new CommandEncoderDescriptor();
                var encoder = engineState._wgpu.DeviceCreateCommandEncoder(engineState._device, in commandEncoderDescriptor);

                var colorAttachment = new RenderPassColorAttachment
                {
                    View = view,
                    ResolveTarget = null,
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.Store,
                    ClearValue = new Color { R = 0.1, G = 0.1, B = 0.1, A = 1 }
                };

                var renderPassDescriptor = new RenderPassDescriptor
                {
                    ColorAttachments = &colorAttachment,
                    ColorAttachmentCount = 1,
                };

                var renderPass = engineState._wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

                renderer.BeginFrame();

                // Draw individual lines
                renderer.DrawLine(new Vector2(-350, -250), new Vector2(-150, -250), new Vector4(1, 0, 0, 1)); // Red horizontal
                renderer.DrawLine(new Vector2(-350, -200), new Vector2(-350, -100), new Vector4(0, 1, 0, 1)); // Green vertical
                renderer.DrawLine(new Vector2(-300, -150), new Vector2(-200, -200), new Vector4(0, 0, 1, 1)); // Blue diagonal

                // Draw a star pattern with lines
                var starCenter = new Vector2(0, -150);
                var starRadius = 100f;
                for (var i = 0; i < 8; i++)
                {
                    var angle = (i / 8f) * MathF.PI * 2;
                    var endpoint = new Vector2(
                        starCenter.X + MathF.Cos(angle) * starRadius,
                        starCenter.Y + MathF.Sin(angle) * starRadius);
                    renderer.DrawLine(starCenter, endpoint, new Vector4(1, 1, 0, 1)); // Yellow
                }

                // Draw a polyline (sine wave)
                var points = new Vector2[50];
                for (var i = 0; i < 50; i++)
                {
                    var x = -350 + (i * 14);
                    var y = 100 + MathF.Sin(i * 0.3f) * 50;
                    points[i] = new Vector2(x, y);
                }
                renderer.DrawPolyline(points, new Vector4(0, 1, 1, 1)); // Cyan

                // Draw a grid
                for (var x = -300; x <= 300; x += 50)
                {
                    renderer.DrawLine(new Vector2(x, 150), new Vector2(x, 250), new Vector4(0.5f, 0.5f, 0.5f, 1));
                }
                for (var y = 150; y <= 250; y += 50)
                {
                    renderer.DrawLine(new Vector2(-300, y), new Vector2(300, y), new Vector4(0.5f, 0.5f, 0.5f, 1));
                }

                renderer.EndFrame(renderPass);

                engineState._wgpu.RenderPassEncoderEnd(renderPass);

                var queue = engineState._wgpu.DeviceGetQueue(engineState._device);
                var commandBufferDescriptor = new CommandBufferDescriptor();
                var commandBuffer = engineState._wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);

                engineState._wgpu.QueueSubmit(queue, 1, &commandBuffer);
                engineState._wgpu.SurfacePresent(engineState._surface);

                engineState._wgpu.CommandBufferRelease(commandBuffer);
                engineState._wgpu.RenderPassEncoderRelease(renderPass);
                engineState._wgpu.CommandEncoderRelease(encoder);
                engineState._wgpu.TextureViewRelease(view);
                engineState._wgpu.TextureRelease(surfaceTexture.Texture);
            };

            window.Run();
        }

        private static Engine2State InitializeEngine(IWindow window)
        {
            var state = new Engine2State { _window = window };
            state._wgpu = WebGPU.GetApi();

            var instanceDescriptor = new InstanceDescriptor();
            state._instance = state._wgpu.CreateInstance(&instanceDescriptor);

            state._surface = window.CreateWebGPUSurface(state._wgpu, state._instance);

            var requestAdapterOptions = new RequestAdapterOptions
            {
                CompatibleSurface = state._surface,
            };

            state._wgpu.InstanceRequestAdapter(
                state._instance,
                in requestAdapterOptions,
                new PfnRequestAdapterCallback((status, adapter, message, userData) =>
                {
                    if (status != RequestAdapterStatus.Success)
                    {
                        throw new Exception($"Unable to create adapter: {SilkMarshal.PtrToString((nint)message)}");
                    }
                    state._adapter = adapter;
                }),
                null
            );

            var deviceDescriptor = new DeviceDescriptor
            {
                DeviceLostCallback = new PfnDeviceLostCallback((reason, message, userData) =>
                {
                    Console.WriteLine($"Device lost! Reason: {reason} Message: {SilkMarshal.PtrToString((nint)message)}");
                }),
            };

            state._wgpu.AdapterRequestDevice(
                state._adapter,
                in deviceDescriptor,
                new PfnRequestDeviceCallback((status, device, message, userData) =>
                {
                    if (status != RequestDeviceStatus.Success)
                    {
                        throw new Exception($"Unable to create device: {SilkMarshal.PtrToString((nint)message)}");
                    }
                    state._device = device;
                }),
                null
            );

            state._wgpu.DeviceSetUncapturedErrorCallback(state._device,
                new PfnErrorCallback((type, message, userData) =>
                {
                    Console.WriteLine($"{type}: {SilkMarshal.PtrToString((nint)message)}");
                }), null);

            state._wgpu.SurfaceGetCapabilities(state._surface, state._adapter, ref state._surfaceCapabilities);

            CreateSwapchain(state);

            return state;
        }

        private static void CreateSwapchain(Engine2State state)
        {
            state._surfaceConfiguration = new SurfaceConfiguration
            {
                Usage = TextureUsage.RenderAttachment,
                Format = state._surfaceCapabilities.Formats[0],
                PresentMode = PresentMode.Fifo,
                Device = state._device,
                Width = (uint)state._window.FramebufferSize.X,
                Height = (uint)state._window.FramebufferSize.Y
            };

            state._wgpu.SurfaceConfigure(state._surface, in state._surfaceConfiguration);
        }

        private static void CleanupEngine(Engine2State state)
        {
            state._wgpu.DeviceRelease(state._device);
            state._wgpu.AdapterRelease(state._adapter);
            state._wgpu.SurfaceRelease(state._surface);
            state._wgpu.InstanceRelease(state._instance);
            state._wgpu.Dispose();
        }
    }
}
