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
    public unsafe class PolygonsCommand : ICommand
    {
        public void Execute(string[] args)
        {
            var options = WindowOptions.Default;
            options.API = GraphicsAPI.None;
            options.Size = new Vector2D<int>(800, 600);
            options.Title = "Radiant - Polygons Demo";
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

                // Row 1: Filled polygons
                renderer.DrawPolygonFilled(-300, -150, 70, 3, new Vector4(1, 0, 0, 1));     // Triangle (red)
                renderer.DrawPolygonFilled(-150, -150, 70, 4, new Vector4(0, 1, 0, 1));     // Square (green)
                renderer.DrawPolygonFilled(0, -150, 70, 5, new Vector4(0, 0, 1, 1));        // Pentagon (blue)
                renderer.DrawPolygonFilled(150, -150, 70, 6, new Vector4(1, 1, 0, 1));      // Hexagon (yellow)
                renderer.DrawPolygonFilled(300, -150, 70, 8, new Vector4(1, 0, 1, 1));      // Octagon (magenta)

                // Row 2: Outlined polygons
                renderer.DrawPolygonOutline(-300, 100, 70, 3, new Vector4(1, 0.5f, 0, 1));  // Triangle outline (orange)
                renderer.DrawPolygonOutline(-150, 100, 70, 4, new Vector4(0.5f, 1, 0, 1));  // Square outline (lime)
                renderer.DrawPolygonOutline(0, 100, 70, 5, new Vector4(0, 0.5f, 1, 1));     // Pentagon outline (sky blue)
                renderer.DrawPolygonOutline(150, 100, 70, 6, new Vector4(1, 1, 0.5f, 1));   // Hexagon outline (light yellow)
                renderer.DrawPolygonOutline(300, 100, 70, 8, new Vector4(1, 0.5f, 1, 1));   // Octagon outline (light magenta)

                // Center: mixed filled and outlined
                renderer.DrawPolygonFilled(0, -25, 50, 7, new Vector4(0.5f, 0.5f, 0.5f, 0.5f)); // Heptagon (semi-transparent gray)
                renderer.DrawPolygonOutline(0, -25, 50, 7, new Vector4(1, 1, 1, 1));            // White outline

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
