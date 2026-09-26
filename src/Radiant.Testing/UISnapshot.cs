using System;
using System.Numerics;
using Radiant.Text;
using Radiant.UI.Core;

namespace Radiant.Testing;

/// <summary>
/// Renders a UI to an image: mounts it at the canvas's size, lets the test act on it (hover,
/// press, focus), steps time on a fixed clock so animations settle the same way every run, and
/// paints it.
/// </summary>
public static class UISnapshot
{
    /// <summary>The clock's step: frames are a sixtieth of a second apart.</summary>
    public const double FrameSeconds = 1 / 60.0;

    /// <summary>Renders <paramref name="element"/> on <paramref name="canvas"/>.</summary>
    /// <param name="canvas">Where to draw, and at what size.</param>
    /// <param name="element">The UI.</param>
    /// <param name="background">What's under it (straight alpha, linear); white by default.</param>
    /// <param name="act">Run once it's laid out, before time moves on (pointer or key input).</param>
    /// <param name="frames">How many frames to step before painting, for animations to finish.</param>
    /// <param name="fonts">The fonts to use; the embedded ones by default.</param>
    public static Snapshot Render(GpuCanvas canvas, Element element, Vector4? background = null, Action<UIRoot>? act = null, int frames = 30, FontLibrary? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(element);
        using var root = new UIRoot(element, fonts);
        root.Update(canvas.Size);
        if (act is not null)
        {
            act(root);
            root.Update(canvas.Size);
        }
        for (var i = 0; i < frames; i++)
        {
            root.Advance(FrameSeconds);
            root.Update(canvas.Size);
        }
        return canvas.Render(background ?? Vector4.One, root.Paint);
    }
}
