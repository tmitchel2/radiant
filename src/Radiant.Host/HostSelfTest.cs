using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Host;

/// <summary>
/// Headless verification that radiant's <c>Renderer2D.DrawImage</c> + <see cref="Texture2D"/>
/// actually work on the GPU (not merely compile): builds a known 2×2 source texture, blits it scaled
/// into an off-screen target alongside a filled "tab" rectangle, reads the result back, and asserts
/// the sampled colours land where expected. Writes a PNG for visual confirmation.
/// </summary>
internal static unsafe class HostSelfTest
{
    public static int Run(string outputPng)
    {
        var w = 256;
        var h = 160;
        var strip = 24;

        using var gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
        var camera = new Camera2D(w, h, Handedness.RightHanded);
        using var renderer = new Renderer2D();
        renderer.Initialize(gpu.State, camera);

        // Source image: 2×2 quadrants R, G, B, white. Texture rows are top-to-bottom; pixel index
        // 0,1 = top row, 2,3 = bottom row. WriteBgra takes (b,g,r,a).
        var src = new byte[2 * 2 * 4];
        WriteBgra(src, 0, 0, 0, 255, 255);     // top-left  red   (r=255)
        WriteBgra(src, 1, 0, 255, 0, 255);     // top-right green (g=255)
        WriteBgra(src, 2, 255, 0, 0, 255);     // bot-left  blue  (b=255)
        WriteBgra(src, 3, 255, 255, 255, 255); // bot-right white
        using var tex = Texture2D.Create(renderer, 2, 2, TextureFormat.Bgra8Unorm);
        tex.Update(src);

        using var rt = new OffscreenReadback(gpu, w, h, TextureFormat.Bgra8UnormSrgb);
        var pixels = rt.RenderAndRead(new Vector4(0.1f, 0.1f, 0.12f, 1f), passPtr =>
        {
            renderer.BeginFrame((uint)w, (uint)h, 1f);
            // Blit the 2×2 image scaled to fill the area below the strip (nearest-ish via linear sampler).
            renderer.DrawImage(tex, 0, strip, w, h - strip);
            // A "tab strip" band on top.
            renderer.DrawRectangleFilled(0, 0, w, strip, new Vector4(0.2f, 0.4f, 0.9f, 1f));
            renderer.EndFrame((RenderPassEncoder*)passPtr);
        });

        SavePng(pixels, w, h, outputPng);

        // Assertions: sample the centre of each image quadrant (in the region below the strip).
        var imgTop = strip;
        var imgH = h - strip;
        var ok = true;
        ok &= CheckQuadrant(pixels, w, "TL/red", w / 4, imgTop + imgH / 4, expectR: true, expectG: false, expectB: false);
        ok &= CheckQuadrant(pixels, w, "TR/green", 3 * w / 4, imgTop + imgH / 4, expectR: false, expectG: true, expectB: false);
        ok &= CheckQuadrant(pixels, w, "BL/blue", w / 4, imgTop + 3 * imgH / 4, expectR: false, expectG: false, expectB: true);
        ok &= CheckQuadrant(pixels, w, "BR/white", 3 * w / 4, imgTop + 3 * imgH / 4, expectR: true, expectG: true, expectB: true);
        // Strip band should be bluish (B dominant, low R).
        ok &= CheckStrip(pixels, w, w / 2, strip / 2);

        Console.WriteLine(ok
            ? $"SELFTEST PASS — DrawImage verified on GPU; wrote {outputPng}"
            : $"SELFTEST FAIL — see {outputPng}");
        return ok ? 0 : 1;
    }

    private static void WriteBgra(byte[] buf, int i, byte b, byte g, byte r, byte a)
    {
        buf[i * 4 + 0] = b;
        buf[i * 4 + 1] = g;
        buf[i * 4 + 2] = r;
        buf[i * 4 + 3] = a;
    }

    private static bool CheckQuadrant(byte[] px, int w, string label, int x, int y, bool expectR, bool expectG, bool expectB)
    {
        var i = (y * w + x) * 4;
        var b = px[i]; var g = px[i + 1]; var r = px[i + 2];
        // sRGB target softens values; use a mid threshold.
        var rHi = r > 110; var gHi = g > 110; var bHi = b > 110;
        var pass = rHi == expectR && gHi == expectG && bHi == expectB;
        Console.WriteLine($"  {label} @({x},{y}) bgra=({b},{g},{r}) -> {(pass ? "ok" : "MISMATCH")}");
        return pass;
    }

    private static bool CheckStrip(byte[] px, int w, int x, int y)
    {
        var i = (y * w + x) * 4;
        var b = px[i]; var r = px[i + 2];
        // Strip is RGBA(0.2,0.4,0.9): blue clearly dominant. (Exact channel values shift under the
        // sRGB target, so assert dominance rather than absolute thresholds.)
        var pass = b > 150 && b > r + 50;
        Console.WriteLine($"  strip @({x},{y}) b={b} r={r} -> {(pass ? "ok" : "MISMATCH")}");
        return pass;
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
