using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;

namespace Radiant.Tests.Graphics2D;

/// <summary>
/// A real GPU render target for tests: a headless device, a <see cref="Renderer2D"/> built for the
/// same sRGB format a window's swapchain gets, and an offscreen target to read frames back from.
/// On a machine with no usable GPU adapter the test is reported inconclusive rather than failed.
/// </summary>
internal sealed unsafe class GpuFrame : IDisposable
{
    /// <summary>Test category for tests that need a GPU; a machine without one can filter them out.</summary>
    public const string Category = "Gpu";

    private readonly OffscreenReadback _target;

    public HeadlessGpu Gpu { get; }
    public Renderer2D Renderer { get; }
    /// <summary>The frame's width in device pixels.</summary>
    public int Width { get; }

    /// <summary>The frame's height in device pixels.</summary>
    public int Height { get; }

    /// <summary>Device pixels per logical unit: 2 draws a logical canvas at Retina density.</summary>
    public float PixelScale { get; }

    private GpuFrame(HeadlessGpu gpu, int width, int height, float pixelScale, uint sampleCount)
    {
        Gpu = gpu;
        PixelScale = pixelScale;
        Width = (int)(width * pixelScale);
        Height = (int)(height * pixelScale);
        Renderer = new Renderer2D();
        Renderer.Initialize(gpu.State, new Camera2D(width, height, Handedness.RightHanded), sampleCount);
        _target = new OffscreenReadback(gpu, Width, Height, TextureFormat.Bgra8UnormSrgb, sampleCount);
    }

    /// <summary>A frame of <paramref name="width"/> × <paramref name="height"/> logical units.</summary>
    public static GpuFrame CreateOrSkip(int width, int height, float pixelScale = 1f, uint sampleCount = 1)
    {
        HeadlessGpu gpu;
        try
        {
            gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Inconclusive($"No GPU available: {ex.Message}");
            throw;
        }
        return new GpuFrame(gpu, width, height, pixelScale, sampleCount);
    }

    /// <summary>Renders one frame over <paramref name="clear"/> and returns its BGRA bytes.</summary>
    /// <param name="clear">The straight-alpha background.</param>
    /// <param name="draw">The frame's drawing.</param>
    /// <param name="clipping">
    /// Whether the frame starts with the clip-aware <c>BeginFrame(width, height, scale)</c>, or the
    /// parameterless <c>BeginFrame()</c> that knows nothing of the attachment.
    /// </param>
    public byte[] Render(Vector4 clear, Action<Renderer2D> draw, bool clipping = true) =>
        _target.RenderAndRead(clear, pass =>
        {
            if (clipping)
            {
                Renderer.BeginFrame((uint)Width, (uint)Height, PixelScale);
            }
            else
            {
                Renderer.BeginFrame();
            }
            draw(Renderer);
            Renderer.EndFrame((RenderPassEncoder*)pass);
        });

    /// <summary>The (R, G, B, A) bytes of one device pixel of a frame from <see cref="Render"/>.</summary>
    public (byte R, byte G, byte B, byte A) PixelAt(byte[] bgra, int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (bgra[i + 2], bgra[i + 1], bgra[i], bgra[i + 3]);
    }

    public void Dispose()
    {
        _target.Dispose();
        Renderer.Dispose();
        Gpu.Dispose();
    }
}
