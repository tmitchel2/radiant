using System;
using Silk.NET.WebGPU;

namespace Radiant.Graphics2D;

/// <summary>
/// Chooses the colour format and alpha mode for a render target from what the surface (or a
/// headless stand-in) reports it supports.
/// <para>
/// <b>One decision, made in one place.</b> The swapchain is configured with the format this returns
/// and <see cref="Renderer2D"/> builds its pipelines against the same call over the same capability
/// list. A pipeline's target format has to match the attachment it draws into, so the two must never
/// be able to disagree. The same choice is what makes a headless render (which reports a single
/// sRGB format) produce the pixels a window would.
/// </para>
/// </summary>
public static unsafe class SurfaceFormats
{
    // Most preferred first. BGRA is the native swapchain order on Metal, D3D12 and most Vulkan
    // drivers; RGBA is the fallback some Vulkan/GL drivers report instead.
    private static ReadOnlySpan<TextureFormat> PreferredSrgbFormats =>
    [
        TextureFormat.Bgra8UnormSrgb,
        TextureFormat.Rgba8UnormSrgb,
    ];

    /// <summary>
    /// The colour format to render into, preferring an <c>*Srgb</c> format so the GPU encodes the
    /// linear colours Radiant draws in and blends in linear light.
    /// </summary>
    /// <param name="available">The formats the surface supports, in the order it reports them.</param>
    /// <returns>
    /// The first preferred sRGB format available; failing that, the surface's first format. That
    /// fallback renders with the wrong transfer curve (too dark), which <see cref="IsSrgb"/> lets a
    /// caller detect and report.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="available"/> is empty.</exception>
    public static TextureFormat ChooseColorFormat(ReadOnlySpan<TextureFormat> available)
    {
        if (available.IsEmpty)
        {
            throw new ArgumentException("The surface reports no supported formats.", nameof(available));
        }

        foreach (var preferred in PreferredSrgbFormats)
        {
            if (available.Contains(preferred))
            {
                return preferred;
            }
        }

        return available[0];
    }

    /// <summary>The colour format for the formats listed in <paramref name="capabilities"/>.</summary>
    public static TextureFormat ChooseColorFormat(SurfaceCapabilities capabilities) =>
        ChooseColorFormat(new ReadOnlySpan<TextureFormat>(capabilities.Formats, (int)capabilities.FormatCount));

    /// <summary>
    /// How the compositor should treat the alpha the renderer writes.
    /// <para>
    /// An opaque window asks for <see cref="CompositeAlphaMode.Opaque"/>, so a stray alpha below one
    /// cannot punch a hole through to the desktop. A transparent window asks for premultiplied
    /// alpha, which is what the renderer produces. On Metal, wgpu reports that mode as
    /// <see cref="CompositeAlphaMode.Unpremultiplied"/>, but Core Animation composites layer
    /// contents as premultiplied either way, so that is the next best. Anything else is left to the
    /// driver.
    /// </para>
    /// </summary>
    public static CompositeAlphaMode ChooseAlphaMode(ReadOnlySpan<CompositeAlphaMode> available, bool transparent)
    {
        ReadOnlySpan<CompositeAlphaMode> preferred = transparent
            ? [CompositeAlphaMode.Premultiplied, CompositeAlphaMode.Unpremultiplied]
            : [CompositeAlphaMode.Opaque];

        foreach (var mode in preferred)
        {
            if (available.Contains(mode))
            {
                return mode;
            }
        }

        return CompositeAlphaMode.Auto;
    }

    /// <summary>The alpha mode for the modes listed in <paramref name="capabilities"/>.</summary>
    public static CompositeAlphaMode ChooseAlphaMode(SurfaceCapabilities capabilities, bool transparent) =>
        ChooseAlphaMode(
            new ReadOnlySpan<CompositeAlphaMode>(capabilities.AlphaModes, (int)capabilities.AlphaModeCount),
            transparent);

    /// <summary>
    /// Whether writes to <paramref name="format"/> are sRGB-encoded by the GPU. Covers the formats a
    /// surface can report; block-compressed sRGB formats are sample-only and never a render target.
    /// </summary>
    public static bool IsSrgb(TextureFormat format) =>
        format is TextureFormat.Bgra8UnormSrgb or TextureFormat.Rgba8UnormSrgb;
}
