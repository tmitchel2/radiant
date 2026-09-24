using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Host;

/// <summary>
/// Headless smoke check of the drag overlay's draw path: renders the ghost chip (via the shared
/// <see cref="HostCompositor.DrawDragGhost"/>) over a transparent target through a real GPU device,
/// reads it back, and asserts the chip coloured the centre. Verifies the overlay's compositing works
/// without needing a display (the live transparent/topmost/click-through window still requires one).
/// </summary>
internal static unsafe class DragOverlaySelfTest
{
    public static int Run(string outputPng)
    {
        var w = 180;
        var h = 30;

        using var gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
        var camera = new Camera2D(w, h, Handedness.RightHanded);
        using var renderer = new Renderer2D();
        renderer.Initialize(gpu.State, camera);
        var font = MsdfFont.LoadEmbedded("default");
        renderer.RegisterMsdfFont(font);

        using var rt = new OffscreenReadback(gpu, w, h, TextureFormat.Bgra8UnormSrgb);
        var pixels = rt.RenderAndRead(new Vector4(0f, 0f, 0f, 0f), passPtr =>
        {
            renderer.BeginFrame((uint)w, (uint)h, 1f);
            HostCompositor.DrawDragGhost(renderer, new Vector2(w / 2f, h / 2f), font, "wt-demo");
            renderer.EndFrame((RenderPassEncoder*)passPtr);
        });
        font.Dispose();

        SavePng(pixels, w, h, outputPng);

        var i = ((h / 2) * w + (w / 2)) * 4;
        var sum = pixels[i] + pixels[i + 1] + pixels[i + 2];
        var ok = sum > 60; // the ghost chip painted the centre (not the transparent cleared background)
        Console.WriteLine(ok
            ? $"DRAG-OVERLAY SELFTEST PASS — ghost chip rendered; wrote {outputPng}"
            : $"DRAG-OVERLAY SELFTEST FAIL — centre still transparent; see {outputPng}");
        return ok ? 0 : 1;
    }

    private static void SavePng(byte[] bgra, int w, int h, string path)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var image = Image.WrapMemory<Bgra32>(bgra, w, h);
        using var stream = File.Create(full);
        image.Save(stream, new PngEncoder());
    }
}
