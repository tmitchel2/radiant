using System;
using System.Diagnostics;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Input;
using Silk.NET.Core.Native;
using Silk.NET.Input;
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
        private Action<double>? _updateCallback;
        private bool _disposed;
        private Handedness _handedness;
        private Vector4 _backgroundColor;
        private RadiantWindowStyle _style = RadiantWindowStyle.Default;
        private readonly Stopwatch _presentStopwatch = new();

        // Input state
        private IInputContext? _inputContext;
        private readonly InputState _inputState = new();

        /// <summary>Gets the current input state.</summary>
        public InputState Input => _inputState;

        /// <summary>Gets the window width in logical pixels.</summary>
        public int WindowWidth => _window?.Size.X ?? 0;

        /// <summary>Gets the window height in logical pixels.</summary>
        public int WindowHeight => _window?.Size.Y ?? 0;

        /// <summary>Gets the framebuffer width in physical pixels (≥ window width on high-DPI displays).</summary>
        public int FramebufferWidth => _window?.FramebufferSize.X ?? 0;

        /// <summary>Gets the framebuffer height in physical pixels (≥ window height on high-DPI displays).</summary>
        public int FramebufferHeight => _window?.FramebufferSize.Y ?? 0;

        /// <summary>Gets the window's top-left x in screen coordinates (logical screen pixels).</summary>
        public int WindowX => _window?.Position.X ?? 0;

        /// <summary>Gets the window's top-left y in screen coordinates (logical screen pixels).</summary>
        public int WindowY => _window?.Position.Y ?? 0;

        /// <summary>
        /// Gets the cursor position in screen coordinates: the window's screen position plus the
        /// window-relative mouse position. Used to hit-test the cursor against other windows' rects
        /// during a cross-window drag. (Logical units; multi-monitor + high-DPI mixing is a known gap.)
        /// </summary>
        public Vector2 GlobalCursor => new(WindowX + _inputState.MousePosition.X, WindowY + _inputState.MousePosition.Y);

        /// <summary>
        /// Wall-clock time of the most recent <c>SurfacePresent</c> call, in milliseconds (typically the
        /// vsync wait). Lets a host thread the real window-present cost back to an offscreen renderer tab
        /// whose own "present" is only an offscreen copy/publish. 0 until the first frame is presented.
        /// </summary>
        public double LastPresentMs { get; private set; }

        /// <summary>Requests the window to close, ending the run loop after the current frame.</summary>
        public void Close() => _window?.Close();

        /// <summary>Move the window's top-left to a screen position (used by the drag overlay to follow the cursor).</summary>
        public void MoveWindow(int x, int y)
        {
            if (_window != null) _window.Position = new Vector2D<int>(x, y);
        }

        /// <summary>Show or hide the window at runtime (the drag overlay hides itself when no drag is active).</summary>
        public void SetVisible(bool visible)
        {
            if (_window != null) _window.IsVisible = visible;
        }

        public void Run(string title, int width, int height, Handedness handedness, Action<Renderer2D> renderCallback, Vector4? backgroundColor = null)
        {
            Run(title, width, height, handedness, renderCallback, null, backgroundColor);
        }

        public void Run(string title, int width, int height, Handedness handedness, Action<Renderer2D> renderCallback, Action<double>? updateCallback, Vector4? backgroundColor, RadiantWindowStyle style)
        {
            _style = style ?? RadiantWindowStyle.Default;
            Run(title, width, height, handedness, renderCallback, updateCallback, backgroundColor);
        }

        public void Run(string title, int width, int height, Handedness handedness, Action<Renderer2D> renderCallback, Action<double>? updateCallback, Vector4? backgroundColor = null)
        {
            _renderCallback = renderCallback;
            _updateCallback = updateCallback;
            _handedness = handedness;
            _backgroundColor = backgroundColor ?? Colors.Neutral900.WithAlpha(1f);

            var options = WindowOptions.Default;
            options.API = GraphicsAPI.None;
            options.Size = new Vector2D<int>(width, height);
            options.Title = title;
            options.IsVisible = _style.Visible;
            options.TopMost = _style.TopMost;
            options.TransparentFramebuffer = _style.Transparent;
            options.WindowBorder = _style.Decorated ? WindowBorder.Resizable : WindowBorder.Hidden;
            options.ShouldSwapAutomatically = false;

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Closing += OnClosing;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Update += OnUpdate;
            _window.Render += OnRender;

            _window.Run();
        }

        private void OnLoad()
        {
            if (_window == null) return;

            _engineState = InitializeEngine(_window);
            _camera = new Camera2D(_window.Size.X, _window.Size.Y, _handedness);
            _renderer = new Renderer2D();
            _renderer.Initialize(_engineState, _camera);

            // Initialize input
            _inputContext = _window.CreateInput();
            InitializeInput();

            if (_style.MousePassthrough)
            {
                TrySetMousePassthrough();
            }
        }

        // GLFW_MOUSE_PASSTHROUGH lets clicks fall through a transparent overlay to the window beneath.
        // Silk.NET 2.22's WindowAttributeSetter enum predates this GLFW 3.4 attribute, so we pass the raw
        // constant. Best-effort: the source window keeps OS pointer capture for a held drag, so a drop
        // still resolves even where passthrough isn't honoured.
        private void TrySetMousePassthrough()
        {
            try
            {
                var handle = _window?.Native?.Glfw;
                if (handle is null) return;
                var glfw = Silk.NET.GLFW.Glfw.GetApi();
                const int GLFW_MOUSE_PASSTHROUGH = 0x0002000D;
                glfw.SetWindowAttrib(
                    (Silk.NET.GLFW.WindowHandle*)handle.Value,
                    (Silk.NET.GLFW.WindowAttributeSetter)GLFW_MOUSE_PASSTHROUGH,
                    true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mouse passthrough unavailable: {ex.Message}");
            }
        }

        private void InitializeInput()
        {
            if (_inputContext == null) return;

            // Set up mouse input
            foreach (var mouse in _inputContext.Mice)
            {
                mouse.MouseMove += OnMouseMove;
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
                mouse.Scroll += OnMouseScroll;
            }

            // Set up keyboard input
            foreach (var keyboard in _inputContext.Keyboards)
            {
                keyboard.KeyDown += OnKeyDown;
                keyboard.KeyUp += OnKeyUp;
                keyboard.KeyChar += OnKeyChar;
            }
        }

        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            var delta = position - _inputState.MousePosition;
            _inputState.MousePosition = position;
            _inputState.MouseDelta = delta;
        }

        private void OnMouseDown(IMouse mouse, MouseButton button)
        {
            _inputState.SetMouseButton(button, true);
        }

        private void OnMouseUp(IMouse mouse, MouseButton button)
        {
            _inputState.SetMouseButton(button, false);
        }

        private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
        {
            _inputState.ScrollDelta = new Vector2(wheel.X, wheel.Y);
        }

        private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
        {
            _inputState.SetKey(key, true);
        }

        private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
        {
            _inputState.SetKey(key, false);
        }

        private void OnKeyChar(IKeyboard keyboard, char character)
        {
            _inputState.LastCharacter = character;
        }

        private void OnUpdate(double delta)
        {
            _updateCallback?.Invoke(delta);
            _inputState.EndFrame();
        }

        private void OnClosing()
        {
            Cleanup();
        }

        private void OnFramebufferResize(Vector2D<int> size)
        {
            if (_disposed || _engineState == null || _camera == null || _window == null) return;

            // The event delivers the PHYSICAL framebuffer size (used to resize the swapchain). The 2D
            // camera, however, is in LOGICAL units — it's created from _window.Size at load, and OnRender
            // applies pixelScale = framebuffer / logical separately. Setting the camera viewport to the
            // physical size here would double its extent on a high-DPI (e.g. 2× Retina) display, mapping
            // logical-coordinate draws to a quarter of the window (half width × half height). Keep the
            // camera logical so it stays consistent with load-time + per-frame scaling.
            CreateSwapchain(_engineState);
            _camera.SetViewportSize(_window.Size.X, _window.Size.Y);
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
                ClearValue = new Color { R = _backgroundColor.X, G = _backgroundColor.Y, B = _backgroundColor.Z, A = _backgroundColor.W }
            };

            var renderPassDescriptor = new RenderPassDescriptor
            {
                ColorAttachments = &colorAttachment,
                ColorAttachmentCount = 1,
            };

            var renderPass = _engineState._wgpu.CommandEncoderBeginRenderPass(encoder, in renderPassDescriptor);

            // Start the frame with the real attachment size (physical framebuffer pixels) and the
            // logical→physical pixel scale. The swapchain is sized to FramebufferSize, while the camera
            // is in logical units, so pixelScale = framebuffer / window. This enables clipping AND gives
            // ApplyScissor a valid full-attachment rect — without it, attachmentWidth/Height default to
            // 0, so the null-clip scissor collapses to (0,0,0,0) and every image / MSDF-text / SDF-shape
            // draw is clipped away (only un-scissored filled rects/lines survived). That left the
            // compositing host window blank below the tab strip.
            var fb = _window!.FramebufferSize;
            var logical = _window.Size;
            var pixelScale = logical.X > 0 ? (float)fb.X / logical.X : 1f;
            _renderer.BeginFrame((uint)fb.X, (uint)fb.Y, pixelScale);

            // User rendering code
            _renderCallback?.Invoke(_renderer);

            _renderer.EndFrame(renderPass);

            _engineState._wgpu.RenderPassEncoderEnd(renderPass);

            var queue = _engineState._wgpu.DeviceGetQueue(_engineState._device);
            var commandBufferDescriptor = new CommandBufferDescriptor();
            var commandBuffer = _engineState._wgpu.CommandEncoderFinish(encoder, in commandBufferDescriptor);

            _engineState._wgpu.QueueSubmit(queue, 1, &commandBuffer);
            _presentStopwatch.Restart();
            _engineState._wgpu.SurfacePresent(_engineState._surface);
            _presentStopwatch.Stop();
            LastPresentMs = _presentStopwatch.Elapsed.TotalMilliseconds;

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

            _inputContext?.Dispose();
            _inputContext = null;

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
