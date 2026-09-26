using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Silk.NET.WebGPU;

namespace Radiant.Tests.Graphics2D;

[TestClass]
public class SurfaceFormatsTests
{
    [TestMethod]
    public void PrefersSrgbEvenWhenTheSurfaceListsALinearFormatFirst()
    {
        // The old code took Formats[0]; a surface listing Bgra8Unorm first rendered too dark.
        ReadOnlySpan<TextureFormat> available = [TextureFormat.Bgra8Unorm, TextureFormat.Bgra8UnormSrgb];

        Assert.AreEqual(TextureFormat.Bgra8UnormSrgb, SurfaceFormats.ChooseColorFormat(available));
    }

    [TestMethod]
    public void PrefersBgraOverRgbaWhenBothAreSrgb()
    {
        ReadOnlySpan<TextureFormat> available = [TextureFormat.Rgba8UnormSrgb, TextureFormat.Bgra8UnormSrgb];

        Assert.AreEqual(TextureFormat.Bgra8UnormSrgb, SurfaceFormats.ChooseColorFormat(available));
    }

    [TestMethod]
    public void TakesRgbaSrgbWhenThatIsTheOnlySrgbFormat()
    {
        ReadOnlySpan<TextureFormat> available = [TextureFormat.Rgba8Unorm, TextureFormat.Rgba8UnormSrgb];

        Assert.AreEqual(TextureFormat.Rgba8UnormSrgb, SurfaceFormats.ChooseColorFormat(available));
    }

    [TestMethod]
    public void FallsBackToTheFirstFormatWhenNoneIsSrgb()
    {
        ReadOnlySpan<TextureFormat> available = [TextureFormat.Rgb10A2Unorm, TextureFormat.Bgra8Unorm];

        var chosen = SurfaceFormats.ChooseColorFormat(available);

        Assert.AreEqual(TextureFormat.Rgb10A2Unorm, chosen);
        Assert.IsFalse(SurfaceFormats.IsSrgb(chosen));
    }

    [TestMethod]
    public void AnEmptyFormatListIsAnError()
    {
        Assert.ThrowsException<ArgumentException>(() => SurfaceFormats.ChooseColorFormat(ReadOnlySpan<TextureFormat>.Empty));
    }

    [TestMethod]
    public unsafe void HeadlessCapabilitiesYieldTheFormatTheyWereGiven()
    {
        // HeadlessGpu synthesises exactly this: one format. The renderer and a window's swapchain both
        // go through ChooseColorFormat, so the headless frame is built for the same target.
        var format = TextureFormat.Bgra8UnormSrgb;
        var capabilities = new SurfaceCapabilities { Formats = &format, FormatCount = 1 };

        Assert.AreEqual(TextureFormat.Bgra8UnormSrgb, SurfaceFormats.ChooseColorFormat(capabilities));
    }

    [TestMethod]
    public void AnOpaqueWindowAsksForOpaqueAlpha()
    {
        ReadOnlySpan<CompositeAlphaMode> available = [CompositeAlphaMode.Unpremultiplied, CompositeAlphaMode.Opaque];

        Assert.AreEqual(CompositeAlphaMode.Opaque, SurfaceFormats.ChooseAlphaMode(available, transparent: false));
    }

    [TestMethod]
    public void ATransparentWindowPrefersPremultipliedAlpha()
    {
        ReadOnlySpan<CompositeAlphaMode> available =
            [CompositeAlphaMode.Opaque, CompositeAlphaMode.Unpremultiplied, CompositeAlphaMode.Premultiplied];

        Assert.AreEqual(CompositeAlphaMode.Premultiplied, SurfaceFormats.ChooseAlphaMode(available, transparent: true));
    }

    [TestMethod]
    public void ATransparentWindowOnMetalTakesTheModeWgpuReportsThere()
    {
        ReadOnlySpan<CompositeAlphaMode> available = [CompositeAlphaMode.Opaque, CompositeAlphaMode.Unpremultiplied];

        Assert.AreEqual(CompositeAlphaMode.Unpremultiplied, SurfaceFormats.ChooseAlphaMode(available, transparent: true));
    }

    [TestMethod]
    public void UnsupportedPreferencesLeaveTheChoiceToTheDriver()
    {
        ReadOnlySpan<CompositeAlphaMode> available = [CompositeAlphaMode.Inherit];

        Assert.AreEqual(CompositeAlphaMode.Auto, SurfaceFormats.ChooseAlphaMode(available, transparent: false));
        Assert.AreEqual(CompositeAlphaMode.Auto, SurfaceFormats.ChooseAlphaMode(available, transparent: true));
    }
}
