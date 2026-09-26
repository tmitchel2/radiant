using System;
using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;

namespace Radiant.Testing;

/// <summary>
/// Somewhere to draw in tests: a headless GPU device, a <see cref="Renderer2D"/> made for the sRGB
/// format a window gets, and an offscreen target read back as a <see cref="Snapshot"/>. Sized in
/// logical units; <see cref="PixelScale"/> 2 draws at Retina density.
/// </summary>
public sealed unsafe class GpuCanvas : IDisposable
{
    private readonly HeadlessGpu _gpu;
    private readonly OffscreenReadback _target;

    private GpuCanvas(HeadlessGpu gpu, int width, int height, float pixelScale, uint sampleCount)
    {
        _gpu = gpu;
        PixelScale = pixelScale;
        Size = new Vector2(width, height);
        Width = (int)(width * pixelScale);
        Height = (int)(height * pixelScale);
        Renderer = new Renderer2D();
        Renderer.Initialize(gpu.State, new Camera2D(width, height, Handedness.RightHanded), sampleCount);
        _target = new OffscreenReadback(gpu, Width, Height, TextureFormat.Bgra8UnormSrgb, sampleCount);
    }

    /// <summary>The renderer that draws into it.</summary>
    public Renderer2D Renderer { get; }

    /// <summary>Its size in logical units.</summary>
    public Vector2 Size { get; }

    /// <summary>Its width in pixels.</summary>
    public int Width { get; }

    /// <summary>Its height in pixels.</summary>
    public int Height { get; }

    /// <summary>Pixels per logical unit.</summary>
    public float PixelScale { get; }

    /// <summary>
    /// A canvas of <paramref name="width"/> × <paramref name="height"/> logical units, or null
    /// where there's no GPU to draw with (a test should then be skipped, not failed).
    /// </summary>
    public static GpuCanvas? TryCreate(int width, int height, float pixelScale = 1f, uint sampleCount = 1)
    {
        HeadlessGpu gpu;
        try
        {
            gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        return new GpuCanvas(gpu, width, height, pixelScale, sampleCount);
    }

    /// <summary>Draws one frame over <paramref name="clear"/> (straight alpha, linear) and reads it back.</summary>
    public Snapshot Render(Vector4 clear, Action<Renderer2D> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        var bgra = _target.RenderAndRead(clear, pass =>
        {
            Renderer.BeginFrame((uint)Width, (uint)Height, PixelScale);
            draw(Renderer);
            Renderer.EndFrame((RenderPassEncoder*)pass);
        });
        for (var i = 0; i < bgra.Length; i += 4)
        {
            (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        }
        return new Snapshot(Width, Height, bgra);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _target.Dispose();
        Renderer.Dispose();
        _gpu.Dispose();
    }
}
