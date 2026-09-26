using System.Numerics;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Frames;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Host;

/// <summary>
/// Headless end-to-end verification of the host's real data path: open a running renderer's
/// cross-process frame buffer, read its latest published frame, upload it to a GPU texture, composite
/// it with a tab strip via <see cref="HostCompositor"/>, and read the result back to a PNG. Proves
/// the shared-memory → texture-upload → DrawImage → strip pipeline works against a real attached
/// renderer, without needing a visible window.
/// </summary>
internal static unsafe class ComposeOnce
{
    // 4K * 4 bytes; the shared buffer is sized for up to this, frames carry their actual dims.
    private const int MaxFrameBytes = 3840 * 2160 * 4;

    public static int Run(string instanceName, string outputPng, int timeoutMs)
    {
        var framesPath = Path.Combine(InstanceRegistry.RootDir, instanceName, "frames.bin");
        if (!File.Exists(framesPath))
        {
            Console.Error.WriteLine($"No frame buffer for instance '{instanceName}' at {framesPath}. Is it running with --attach?");
            return 2;
        }

        using var reader = SharedFrameBuffer.OpenReader(framesPath);

        var dst = new byte[MaxFrameBytes];
        FrameInfo info = default;
        var got = false;
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (reader.TryRead(dst, out info))
            {
                got = true;
                break;
            }
            Thread.Sleep(8);
        }
        if (!got)
        {
            Console.Error.WriteLine($"No frame published by '{instanceName}' within {timeoutMs} ms.");
            return 3;
        }

        var fw = info.Width;
        var fh = info.Height;
        var viewW = fw;
        var viewH = fh + (int)HostCompositor.StripHeight;

        using var gpu = new HeadlessGpu(TextureFormat.Bgra8UnormSrgb);
        var camera = new Camera2D(viewW, viewH, Handedness.RightHanded);
        using var renderer = new Renderer2D();
        renderer.Initialize(gpu.State, camera);

        using var font = MsdfFont.LoadEmbedded(EmbeddedFonts.Default);
        renderer.RegisterMsdfFont(font);

        // sRGB so sampling decodes the renderer's sRGB-encoded readback bytes (matches LiveHost).
        using var tex = Texture2D.Create(renderer, fw, fh, TextureFormat.Bgra8UnormSrgb);
        tex.Update(dst.AsSpan(0, fw * fh * 4));

        var tabs = new[] { instanceName, "second-tab" };

        using var rt = new OffscreenReadback(gpu, viewW, viewH, TextureFormat.Bgra8UnormSrgb);
        var pixels = rt.RenderAndRead(new Vector4(0.05f, 0.05f, 0.06f, 1f), passPtr =>
        {
            renderer.BeginFrame((uint)viewW, (uint)viewH, 1f);
            HostCompositor.Draw(renderer, tex, viewW, viewH, tabs, activeIndex: 0, hoveredIndex: -1, reorder: null, framePixelScale: 0f, font);
            renderer.EndFrame((RenderPassEncoder*)passPtr);
        });

        SavePng(pixels, viewW, viewH, outputPng);

        Console.WriteLine($"COMPOSE-ONCE OK — composited frame #{info.FrameIndex} ({fw}x{fh}) from '{instanceName}' + tab strip -> {Path.GetFullPath(outputPng)}");
        return 0;
    }

    private static void SavePng(byte[] bgra, int w, int h, string path)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        using var image = Image.WrapMemory<Bgra32>(bgra, w, h);
        using var stream = File.Create(full);
        image.Save(stream, new PngEncoder());
    }
}
