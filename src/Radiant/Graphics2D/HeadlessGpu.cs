using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using WgpuExtensions = Silk.NET.WebGPU.Extensions.WGPU.Wgpu;

namespace Radiant.Graphics2D;

/// <summary>
/// Creates a windowless WebGPU device wrapped in a radiant <see cref="Engine2State"/> so a
/// <see cref="Renderer2D"/> can render to an off-screen target with no OS window. Used by tab
/// renderers, the host self-tests and GPU tests to render (and read back) real frames.
/// <para>
/// The synthesised capability list holds one format, and <see cref="SurfaceFormats"/> makes the same
/// choice from it that it makes for a window's surface. With the default sRGB format, a headless frame
/// is rendered exactly as a window would render it.
/// </para>
/// </summary>
public sealed unsafe class HeadlessGpu : IDisposable
{
    public Engine2State State { get; }
    public WgpuExtensions? Ext { get; }
    public WebGPU Wgpu => State._wgpu;
    public Device* Device => State._device;
    public Queue* Queue { get; }

    private readonly TextureFormat* _formats;
    private bool _disposed;

    /// <summary>Creates the device, or throws if the machine has no usable GPU adapter.</summary>
    /// <param name="format">The one render-target format pipelines are built for.</param>
    /// <exception cref="InvalidOperationException">No adapter or device could be created.</exception>
    public HeadlessGpu(TextureFormat format = TextureFormat.Bgra8UnormSrgb)
    {
        var state = new Engine2State { _wgpu = WebGPU.GetApi() };

        var instanceDescriptor = new InstanceDescriptor();
        state._instance = state._wgpu.CreateInstance(&instanceDescriptor);

        // THE CALLBACKS ONLY RECORD; THE THROW HAPPENS BACK IN MANAGED CODE. wgpu invokes them
        // synchronously from inside the native call, and an exception unwinding through native frames
        // takes the process down instead of reaching the caller. A machine with no GPU (a CI runner, a
        // VM) has to surface as an ordinary exception so a test can skip rather than crash.
        string? failure = null;
        var adapterOptions = new RequestAdapterOptions(); // no CompatibleSurface => headless
        state._wgpu.InstanceRequestAdapter(state._instance, in adapterOptions,
            new PfnRequestAdapterCallback((status, adapter, message, _) =>
            {
                if (status == RequestAdapterStatus.Success)
                {
                    state._adapter = adapter;
                }
                else
                {
                    failure = $"Unable to create headless adapter ({status}): {SilkMarshal.PtrToString((nint)message)}";
                }
            }), null);
        if (state._adapter == null)
        {
            state._wgpu.InstanceRelease(state._instance);
            state._wgpu.Dispose();
            throw new InvalidOperationException(failure ?? "Unable to create headless adapter.");
        }

        var deviceDescriptor = new DeviceDescriptor
        {
            DeviceLostCallback = new PfnDeviceLostCallback((reason, message, _) =>
                Console.WriteLine($"Device lost! Reason: {reason} Message: {SilkMarshal.PtrToString((nint)message)}")),
        };
        state._wgpu.AdapterRequestDevice(state._adapter, in deviceDescriptor,
            new PfnRequestDeviceCallback((status, device, message, _) =>
            {
                if (status == RequestDeviceStatus.Success)
                {
                    state._device = device;
                }
                else
                {
                    failure = $"Unable to create headless device ({status}): {SilkMarshal.PtrToString((nint)message)}";
                }
            }), null);
        if (state._device == null)
        {
            state._wgpu.AdapterRelease(state._adapter);
            state._wgpu.InstanceRelease(state._instance);
            state._wgpu.Dispose();
            throw new InvalidOperationException(failure ?? "Unable to create headless device.");
        }

        state._wgpu.DeviceSetUncapturedErrorCallback(state._device,
            new PfnErrorCallback((type, message, _) =>
                Console.WriteLine($"{type}: {SilkMarshal.PtrToString((nint)message)}")), null);

        _ = state._wgpu.TryGetDeviceExtension(state._device, out WgpuExtensions? ext);
        Ext = ext;

        // Synthesise a one-entry capability list so Renderer2D.Initialize builds its pipelines against
        // a known format with no surface present.
        _formats = (TextureFormat*)NativeMemory.Alloc((nuint)sizeof(TextureFormat));
        _formats[0] = format;
        state._surfaceCapabilities = new SurfaceCapabilities { Formats = _formats, FormatCount = 1 };

        State = state;
        Queue = state._wgpu.DeviceGetQueue(state._device);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var wgpu = State._wgpu;
        if (State._device != null) wgpu.DeviceRelease(State._device);
        if (State._adapter != null) wgpu.AdapterRelease(State._adapter);
        if (State._instance != null) wgpu.InstanceRelease(State._instance);
        if (_formats != null) NativeMemory.Free(_formats);
        wgpu.Dispose();
    }
}
