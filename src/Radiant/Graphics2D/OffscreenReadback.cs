using System;
using System.Numerics;
using Silk.NET.WebGPU;
using WgpuExtensions = Silk.NET.WebGPU.Extensions.WGPU.Wgpu;

namespace Radiant.Graphics2D;

/// <summary>
/// Renders a single frame into an off-screen colour texture and reads the pixels back to a tightly
/// packed (no row padding) byte[]. Lets the host's GPU draw path be verified with an image even
/// though no window is visible in headless/CI contexts. Mirrors the readback a renderer uses for
/// screenshots and offscreen presentation.
/// </summary>
public sealed unsafe class OffscreenReadback : IDisposable
{
    private readonly WebGPU _wgpu;
    private readonly WgpuExtensions? _ext;
    private readonly Device* _device;
    private readonly Queue* _queue;

    public int Width { get; }
    public int Height { get; }

    /// <summary>The texture the pixels are read out of. Single-sampled, always.</summary>
    public Texture* ColorTexture { get; }

    /// <summary>A view of it.</summary>
    public TextureView* ColorView { get; }

    /// <summary>Samples per pixel the pass draws at. One means no multisampling.</summary>
    public uint SampleCount { get; }

    // The attachment drawn INTO when multisampling, resolved down into ColorTexture at the end of
    // the pass. Null at one sample, where the pass draws straight into ColorTexture.
    //
    // TWO TEXTURES, BECAUSE A MULTISAMPLED ONE CANNOT BE COPIED FROM. Its usage may not include
    // CopySrc, so the readback has to come from the resolved single-sample texture -- which is what
    // ResolveTarget on the attachment exists to fill.
    private readonly Texture* _samples;
    private readonly TextureView* _samplesView;

    public OffscreenReadback(
        HeadlessGpu gpu,
        int width,
        int height,
        TextureFormat format = TextureFormat.Bgra8UnormSrgb,
        uint sampleCount = 1)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        _wgpu = gpu.Wgpu;
        _ext = gpu.Ext;
        _device = gpu.Device;
        _queue = gpu.Queue;
        Width = width;
        Height = height;
        SampleCount = Math.Max(1u, sampleCount);

        var desc = new TextureDescriptor
        {
            Size = new Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = TextureDimension.Dimension2D,
            Format = format,
            Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
        };
        ColorTexture = _wgpu.DeviceCreateTexture(_device, in desc);
        ColorView = _wgpu.TextureCreateView(ColorTexture, null);

        if (SampleCount == 1)
        {
            return;
        }

        var multisampled = desc with
        {
            SampleCount = SampleCount,
            Usage = TextureUsage.RenderAttachment,
        };

        _samples = _wgpu.DeviceCreateTexture(_device, in multisampled);
        _samplesView = _wgpu.TextureCreateView(_samples, null);
    }

    /// <summary>
    /// Begin a render pass into the off-screen colour view (clearing to <paramref name="clear"/>, a
    /// straight-alpha linear colour like every other colour the renderer takes),
    /// invoke <paramref name="draw"/> (which receives the render-pass encoder pointer as an nint)
    /// to encode draws, end the pass, copy to a staging buffer, submit, and return the tightly packed
    /// pixels in the texture's byte order.
    /// </summary>
    public byte[] RenderAndRead(Vector4 clear, Action<nint> draw)
    {
        var pixels = new byte[Width * Height * 4];
        RenderAndRead(clear, draw, pixels);
        return pixels;
    }

    /// <summary>
    /// <see cref="RenderAndRead(Vector4, Action{nint})"/> into <paramref name="pixels"/>
    /// (<see cref="Width"/> × <see cref="Height"/> × 4 bytes), so a caller reading every frame
    /// reuses one buffer instead of allocating a large one each time.
    /// </summary>
    public void RenderAndRead(Vector4 clear, Action<nint> draw, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != Width * Height * 4)
        {
            throw new ArgumentException($"Expected {Width * Height * 4} bytes, got {pixels.Length}.", nameof(pixels));
        }
        var encoderDesc = new CommandEncoderDescriptor();
        var encoder = _wgpu.DeviceCreateCommandEncoder(_device, in encoderDesc);

        // Multisampled: draw into the sample texture and resolve into the one that can be copied.
        // Single-sampled: draw straight into it and resolve nothing, which is what every caller that
        // does not ask for samples gets, byte for byte as before.
        var colorAttachment = new RenderPassColorAttachment
        {
            View = SampleCount == 1 ? ColorView : _samplesView,
            ResolveTarget = SampleCount == 1 ? null : ColorView,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = ClearColor.FromStraightAlpha(clear),
        };
        var passDesc = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &colorAttachment };
        var pass = _wgpu.CommandEncoderBeginRenderPass(encoder, in passDesc);
        // The encoder pointer is handed over as nint; callers (in their own unsafe context) cast it
        // back to RenderPassEncoder*. Avoids taking the address of a by-value lambda parameter.
        draw((nint)pass);
        _wgpu.RenderPassEncoderEnd(pass);
        _wgpu.RenderPassEncoderRelease(pass);

        var bytesPerRow = ((uint)Width * 4 + 255) & ~255u;
        var bufferSize = (ulong)bytesPerRow * (ulong)Height;
        var stagingDesc = new BufferDescriptor
        {
            Size = bufferSize,
            Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
            MappedAtCreation = false,
        };
        var staging = _wgpu.DeviceCreateBuffer(_device, in stagingDesc);

        var src = new ImageCopyTexture { Texture = ColorTexture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
        var dst = new ImageCopyBuffer
        {
            Buffer = staging,
            Layout = new TextureDataLayout { Offset = 0, BytesPerRow = bytesPerRow, RowsPerImage = (uint)Height },
        };
        var copySize = new Extent3D { Width = (uint)Width, Height = (uint)Height, DepthOrArrayLayers = 1 };
        _wgpu.CommandEncoderCopyTextureToBuffer(encoder, in src, in dst, in copySize);

        var cmdDesc = new CommandBufferDescriptor();
        var cmd = _wgpu.CommandEncoderFinish(encoder, in cmdDesc);
        _wgpu.QueueSubmit(_queue, 1, &cmd);

        var done = false;
        _wgpu.QueueOnSubmittedWorkDone(_queue, new PfnQueueWorkDoneCallback((_, _) => done = true), null);
        while (!done) _ = _ext?.DevicePoll(_device, false, null);

        var mapped = false;
        _wgpu.BufferMapAsync(staging, MapMode.Read, 0, (nuint)bufferSize,
            new PfnBufferMapCallback((_, _) => mapped = true), null);
        while (!mapped) _ = _ext?.DevicePoll(_device, false, null);

        var srcPtr = (byte*)_wgpu.BufferGetMappedRange(staging, 0, (nuint)bufferSize);
        var rowBytes = Width * 4;
        for (var row = 0; row < Height; row++)
        {
            new ReadOnlySpan<byte>(srcPtr + (long)row * bytesPerRow, rowBytes).CopyTo(pixels.AsSpan(row * rowBytes, rowBytes));
        }
        _wgpu.BufferUnmap(staging);

        _wgpu.BufferRelease(staging);
        _wgpu.CommandBufferRelease(cmd);
        _wgpu.CommandEncoderRelease(encoder);
    }

    public void Dispose()
    {
        if (_samplesView != null) _wgpu.TextureViewRelease(_samplesView);
        if (_samples != null) _wgpu.TextureRelease(_samples);
        if (ColorView != null) _wgpu.TextureViewRelease(ColorView);
        if (ColorTexture != null) _wgpu.TextureRelease(ColorTexture);
    }
}
