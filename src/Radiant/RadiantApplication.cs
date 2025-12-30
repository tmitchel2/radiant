using System;
using Radiant.Graphics2D;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace Radiant
{
    public unsafe class RadiantApplication : IDisposable
    {
        private IWindow? _window;
        private Engine2State? _engineState;
        private Renderer2D? _renderer;
        private Camera2D? _camera;
        private Action<Renderer2D>? _renderCallback;
        private bool _disposed;

        public void Run(string title, int width, int height, Action<Renderer2D> renderCallback)
        {
            _renderCallback = renderCallback;

            var options = WindowOptions.Default;
            options.API = GraphicsAPI.None;
            options.Size = new Vector2D<int>(width, height);
            options.Title = title;
            options.IsVisible = true;
            options.ShouldSwapAutomatically = false;

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Closing += OnClosing;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Render += OnRender;

            _window.Run();
        }

        private void OnLoad()
        {
            if (_window == null) return;

            _engineState = InitializeEngine(_window);
            _camera = new Camera2D(_window.Size.X, _window.Size.Y);
            _renderer = new Renderer2D();
            _renderer.Initialize(_engineState, _camera);
        }

        private void OnClosing()
        {
            Cleanup();
        }

        private void OnFramebufferResize(Vector2D<int> size)
        {
            if (_disposed || _engineState == null || _camera == null) return;

            CreateSwapchain(_engineState);
            _camera.SetViewportSize(size.X, size.Y);
        }

        private void OnRender(double delta)
        {
            if (_disposed || _engineState == null || _renderer == null) return;

            SurfaceTexture surfaceTexture;
            _engineState._wgpu.SurfaceGetCurrentTexture(_engineState._surface, &surfaceTexture);

            switch (surfaceTexture.Status)
            {
                case SurfaceGetCurrentTextureStatus.Timeout:
                case SurfaceGetCurrentTextureStatus.Outdated:
                case SurfaceGetCurrentTextureStatus.Lost:
                    _engineState._wgpu.TextureRelease(surfaceTexture.Texture);
                    CreateSwapchain(_engineState);
                    return;
                case SurfaceGetCurrentTextureStatus.OutOfMemory:
                case SurfaceGetCurrentTextureStatus.DeviceLost:
                    throw new InvalidOperationException($"Surface error: {surfaceTexture.Status}");
            }

            var view = _engineState._wgpu.TextureCreateView(surfaceTexture.Texture, null);
            var commandEncoderDescriptor = new CommandEncoderDescriptor();
            var encoder = _engineState._wgpu.DeviceCreateCommandEncoder(_engineState._device, in commandEncoderDescriptor);

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

            var renderPass = _engineState._wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

            _renderer.BeginFrame();

            // User rendering code
            _renderCallback?.Invoke(_renderer);

            _renderer.EndFrame(renderPass);

            _engineState._wgpu.RenderPassEncoderEnd(renderPass);

            var queue = _engineState._wgpu.DeviceGetQueue(_engineState._device);
            var commandBufferDescriptor = new CommandBufferDescriptor();
            var commandBuffer = _engineState._wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);

            _engineState._wgpu.QueueSubmit(queue, 1, &commandBuffer);
            _engineState._wgpu.SurfacePresent(_engineState._surface);

            _engineState._wgpu.CommandBufferRelease(commandBuffer);
            _engineState._wgpu.RenderPassEncoderRelease(renderPass);
            _engineState._wgpu.CommandEncoderRelease(encoder);
            _engineState._wgpu.TextureViewRelease(view);
            _engineState._wgpu.TextureRelease(surfaceTexture.Texture);
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
                        throw new InvalidOperationException($"Unable to create adapter: {SilkMarshal.PtrToString((nint)message)}");
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
                        throw new InvalidOperationException($"Unable to create device: {SilkMarshal.PtrToString((nint)message)}");
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

        private void Cleanup()
        {
            if (_disposed) return;
            _disposed = true;

            _renderer?.Dispose();
            _renderer = null;

            if (_engineState != null)
            {
                CleanupEngine(_engineState);
                _engineState = null;
            }

            _camera = null;
        }

        public void Dispose()
        {
            Cleanup();
            _window?.Dispose();
            _window = null;
            GC.SuppressFinalize(this);
        }
    }
}
