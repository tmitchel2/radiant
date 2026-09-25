using Radiant.Host.AgentControlProtocol;
using Radiant.Host.Ipc.Frames;
using Radiant.Host.Ipc.Input;

namespace Radiant.Host;

/// <summary>
/// Headless verification that a running <c>--attach</c> renderer actually consumes host input from
/// the <see cref="InputRing"/>: capture a frame, drive a left-mouse-drag (which orbits the camera)
/// through the ring, capture another frame, and assert the pixels changed. Proves the renderer half
/// of input forwarding works without needing a visible window.
/// </summary>
internal static class InputTest
{
    private const int MaxFrameBytes = 3840 * 2160 * 4;
    private const int MouseButtonLeft = 0; // Silk.NET.Input.MouseButton.Left

    public static int Run(string instanceName, int timeoutMs)
    {
        var dir = Path.Combine(InstanceRegistry.RootDir, instanceName);
        var framesPath = Path.Combine(dir, "frames.bin");
        if (!File.Exists(framesPath))
        {
            Console.Error.WriteLine($"No frame buffer for '{instanceName}'. Is it running with --attach?");
            return 2;
        }

        using var frames = SharedFrameBuffer.OpenReader(framesPath);
        var bufA = new byte[MaxFrameBytes];
        var bufB = new byte[MaxFrameBytes];

        if (!WaitForFrame(frames, bufA, out var infoA, timeoutMs))
        {
            Console.Error.WriteLine("No initial frame published.");
            return 3;
        }

        var inputPath = Path.Combine(dir, "input.bin");
        using var input = InputRing.CreateWriter(inputPath);
        {
            input.SetSize(infoA.Width, infoA.Height);
            input.SetFocus(true);

            // Centre the cursor, press left, then sweep right — the renderer reads the latest mouse
            // position each frame, derives a delta, and orbits the camera while the button is held.
            var cx = infoA.Width / 2f;
            var cy = infoA.Height / 2f;
            input.SetMousePosition(cx, cy);
            input.Push(new InputEvent(InputEventKind.MouseDown, MouseButtonLeft, cx, cy));

            for (var i = 0; i < 60; i++)
            {
                input.SetMousePosition(cx + i * 6f, cy);
                Thread.Sleep(16);
            }

            input.Push(new InputEvent(InputEventKind.MouseUp, MouseButtonLeft, cx + 360f, cy));
            Thread.Sleep(100);

            // Read a fresh frame after the drag.
            if (!WaitForNewFrame(frames, bufB, infoA.FrameIndex, out var infoB, timeoutMs))
            {
                Console.Error.WriteLine("No frame after input.");
                return 3;
            }

            var changed = CountDifferingPixels(bufA, bufB, infoB.Width, infoB.Height);
            var totalPixels = infoB.Width * infoB.Height;
            var pct = 100.0 * changed / totalPixels;
            // A left-drag orbits the camera; the rendered frame must change as a result. The default
            // demo scene loads no geometry under the headless renderer (scripting host unavailable),
            // so the visible moving content is the navigation gizmo — still a few hundred px. Use an
            // absolute floor well above frame-to-frame noise rather than a percentage.
            var ok = changed > 200;
            Console.WriteLine(ok
                ? $"INPUT-TEST PASS — left-drag via InputRing orbited the camera: {changed} px changed ({pct:F2}%)"
                : $"INPUT-TEST FAIL — frame unchanged: {changed} px ({pct:F2}%); renderer not consuming input?");
            return ok ? 0 : 1;
        }
    }

    private static bool WaitForFrame(SharedFrameBuffer frames, byte[] dst, out FrameInfo info, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (frames.TryRead(dst, out info))
            {
                return true;
            }
            Thread.Sleep(8);
        }
        info = default;
        return false;
    }

    private static bool WaitForNewFrame(SharedFrameBuffer frames, byte[] dst, long afterIndex, out FrameInfo info, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (frames.TryRead(dst, out info) && info.FrameIndex > afterIndex)
            {
                return true;
            }
            Thread.Sleep(8);
        }
        info = default;
        return false;
    }

    private static int CountDifferingPixels(byte[] a, byte[] b, int width, int height)
    {
        var count = 0;
        var n = width * height;
        for (var i = 0; i < n; i++)
        {
            var o = i * 4;
            // Compare RGB with a small tolerance to ignore dithering noise.
            if (Math.Abs(a[o] - b[o]) > 8 || Math.Abs(a[o + 1] - b[o + 1]) > 8 || Math.Abs(a[o + 2] - b[o + 2]) > 8)
            {
                count++;
            }
        }
        return count;
    }
}
