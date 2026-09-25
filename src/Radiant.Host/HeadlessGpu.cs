using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using WgpuExtensions = Silk.NET.WebGPU.Extensions.WGPU.Wgpu;

namespace Radiant.Host;

/// <summary>
/// Creates a windowless WebGPU device wrapped in a radiant <see cref="Engine2State"/> so a
/// <see cref="Radiant.Graphics2D.Renderer2D"/> can render to an off-screen target with no OS window.
/// Used by the host self-test (and any headless host tooling) to validate the compositing path.
/// Mirrors the headless init a renderer uses in its <c>--attach</c> mode.
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

    public HeadlessGpu(TextureFormat format = TextureFormat.Bgra8UnormSrgb)
    {
        var state = new Engine2State { _wgpu = WebGPU.GetApi() };

        var instanceDescriptor = new InstanceDescriptor();
        state._instance = state._wgpu.CreateInstance(&instanceDescriptor);

        var adapterOptions = new RequestAdapterOptions(); // no CompatibleSurface => headless
        state._wgpu.InstanceRequestAdapter(state._instance, in adapterOptions,
            new PfnRequestAdapterCallback((status, adapter, message, _) =>
            {
                if (status != RequestAdapterStatus.Success)
                    throw new InvalidOperationException($"Unable to create headless adapter: {SilkMarshal.PtrToString((nint)message)}");
                state._adapter = adapter;
            }), null);

        var deviceDescriptor = new DeviceDescriptor
        {
            DeviceLostCallback = new PfnDeviceLostCallback((reason, message, _) =>
                Console.WriteLine($"Device lost! Reason: {reason} Message: {SilkMarshal.PtrToString((nint)message)}")),
        };
        state._wgpu.AdapterRequestDevice(state._adapter, in deviceDescriptor,
            new PfnRequestDeviceCallback((status, device, message, _) =>
            {
                if (status != RequestDeviceStatus.Success)
                    throw new InvalidOperationException($"Unable to create headless device: {SilkMarshal.PtrToString((nint)message)}");
                state._device = device;
            }), null);

        state._wgpu.DeviceSetUncapturedErrorCallback(state._device,
            new PfnErrorCallback((type, message, _) =>
                Console.WriteLine($"{type}: {SilkMarshal.PtrToString((nint)message)}")), null);

        _ = state._wgpu.TryGetDeviceExtension(state._device, out WgpuExtensions? ext);
        Ext = ext;

        // Synthesise a one-entry capability list so Renderer2D.Initialize (which reads Formats[0])
        // builds its pipelines against a known format with no surface present.
        _formats = (TextureFormat*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)sizeof(TextureFormat));
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
        if (_formats != null) System.Runtime.InteropServices.NativeMemory.Free(_formats);
        wgpu.Dispose();
    }
}
